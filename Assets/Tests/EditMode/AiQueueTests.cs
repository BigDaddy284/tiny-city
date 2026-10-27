using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation.AI;

namespace TinyTown.Tests.EditMode
{
    public class AiQueueTests
    {
        private FakeAiTransport transport;
        private AiRequestQueue queue;
        private List<string> handled;
        private List<string> gaveUp;

        [SetUp]
        public void SetUp()
        {
            transport = new FakeAiTransport();
            queue = new AiRequestQueue(transport, "http://ollama/api/chat", timeoutSeconds: 10, maxAttempts: 2, maxPending: 4);
            handled = new List<string>();
            gaveUp = new List<string>();
        }

        private AiRequest Request(string key, AiPriority priority = AiPriority.Decision, System.Func<string, AiReplyCheck> handle = null, System.Func<bool> wanted = null, string residentId = "res-0001")
        {
            return new AiRequest(
                key,
                residentId,
                priority,
                key,
                "{\"key\":\"" + key + "\"}",
                handle ?? (text =>
                {
                    handled.Add(key);
                    return AiReplyCheck.Accepted();
                }),
                wanted,
                reason => gaveUp.Add(key + ": " + reason));
        }

        [Test]
        public void ResidentJustServed_WaitsBehindOthersOfTheSamePriority()
        {
            queue.Enqueue(Request("mabel-1", residentId: "res-0001"), 0);
            queue.Update(0);
            queue.Enqueue(Request("mabel-2", residentId: "res-0001"), 0.5);
            queue.Enqueue(Request("arthur-1", residentId: "res-0002"), 1);
            transport.Calls[0].Succeed("{}");
            queue.Update(1.5);

            Assert.That(transport.Calls[1].Body, Does.Contain("arthur-1"), "Mabel asked first, but she was just served");

            transport.Calls[1].Succeed("{}");
            queue.Update(2);
            Assert.That(transport.Calls[2].Body, Does.Contain("mabel-2"));
        }

        [Test]
        public void JustServedResident_StillBeatsLessImportantRequests()
        {
            queue.Enqueue(Request("mabel-decision", residentId: "res-0001"), 0);
            queue.Update(0);
            queue.Enqueue(Request("mabel-urgent", AiPriority.Urgent, residentId: "res-0001"), 0.5);
            queue.Enqueue(Request("arthur-chat", AiPriority.Dialogue, residentId: "res-0002"), 0.5);
            transport.Calls[0].Succeed("{}");
            queue.Update(1);

            Assert.That(transport.Calls[1].Body, Does.Contain("mabel-urgent"));
        }

        [Test]
        public void SendsOnlyOneRequestAtATime()
        {
            queue.Enqueue(Request("a"), 0);
            queue.Enqueue(Request("b"), 0);
            queue.Update(0);
            queue.Update(1);

            Assert.That(transport.Calls.Count, Is.EqualTo(1));
            Assert.That(queue.PendingCount, Is.EqualTo(1));

            transport.Calls[0].Succeed("{}");
            queue.Update(2);

            Assert.That(handled, Is.EqualTo(new[] { "a" }));
            Assert.That(transport.Calls.Count, Is.EqualTo(2));
            Assert.That(queue.Stats.LastLatencySeconds, Is.EqualTo(2).Within(0.001));
        }

        [Test]
        public void HigherPriorityIsServedFirst()
        {
            queue.Enqueue(Request("flavour", AiPriority.Flavour), 0);
            queue.Enqueue(Request("reflection", AiPriority.Reflection), 0);
            queue.Enqueue(Request("urgent", AiPriority.Urgent), 0);
            queue.Update(0);

            Assert.That(transport.Calls[0].Body, Does.Contain("urgent"));
        }

        [Test]
        public void LongWaitingRequestsAge_SoLowPrioritiesAreNotStarved()
        {
            queue.Enqueue(Request("old-flavour", AiPriority.Flavour), 0);
            queue.Enqueue(Request("busy", AiPriority.Decision), 0);
            queue.Update(0);
            transport.Calls[0].Succeed("{}");

            // A fresh decision arrives long after the flavour request started waiting.
            var later = AiRequestQueue.AgingSecondsPerClass * 3;
            queue.Enqueue(Request("new-decision", AiPriority.Decision), later);
            queue.Update(later);

            Assert.That(transport.Calls[1].Body, Does.Contain("old-flavour"));
        }

        [Test]
        public void SameKey_ReplacesTheWaitingRequest()
        {
            queue.Enqueue(Request("busy"), 0);
            queue.Update(0);
            queue.Enqueue(Request("decision:res-0001"), 0);
            queue.Enqueue(new AiRequest("decision:res-0001", "res-0001", AiPriority.Decision, "newer", "{\"newer\":true}", _ => AiReplyCheck.Accepted()), 0);

            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(queue.Pending[0].Body, Does.Contain("newer"));
        }

        [Test]
        public void Timeout_IsRetriedOnce_ThenGivenUp()
        {
            queue.Enqueue(Request("slow"), 0);
            queue.Update(0);
            queue.Update(11);
            Assert.That(transport.Calls[0].Cancelled, Is.True);

            queue.Update(11 + AiRequestQueue.RetryDelaySeconds);
            Assert.That(transport.Calls.Count, Is.EqualTo(2));

            queue.Update(30);
            queue.Update(60);

            Assert.That(transport.Calls.Count, Is.EqualTo(2), "no endless retries");
            Assert.That(queue.Stats.TimedOut, Is.EqualTo(2));
            Assert.That(gaveUp.Count, Is.EqualTo(1));
            Assert.That(gaveUp[0], Does.Contain("no answer within"));
            Assert.That(queue.Active, Is.Null);
        }

        [Test]
        public void InvalidReply_IsRetried_ThenGivenUp()
        {
            var attempts = 0;
            queue.Enqueue(Request("bad", handle: _ =>
            {
                attempts++;
                return AiReplyCheck.Invalid("not JSON");
            }), 0);

            transport.Responder = call => call.Succeed("nonsense");
            for (var t = 0; t < 20; t++)
            {
                queue.Update(t);
            }

            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(queue.Stats.InvalidReplies, Is.EqualTo(2));
            Assert.That(gaveUp.Single(), Does.Contain("not JSON"));
        }

        [Test]
        public void HandlerException_CountsAsInvalid_AndNeverEscapes()
        {
            queue.Enqueue(Request("throws", handle: _ => throw new System.InvalidOperationException("boom")), 0);
            transport.Responder = call => call.Succeed("{}");

            Assert.DoesNotThrow(() =>
            {
                for (var t = 0; t < 10; t++)
                {
                    queue.Update(t);
                }
            });
            Assert.That(gaveUp.Single(), Does.Contain("boom"));
        }

        [Test]
        public void TransportException_IsRetried_AndNeverEscapes()
        {
            transport.Throws = 5;
            queue.Enqueue(Request("x"), 0);

            Assert.DoesNotThrow(() =>
            {
                for (var t = 0; t < 10; t++)
                {
                    queue.Update(t);
                }
            });
            Assert.That(queue.Stats.TransportErrors, Is.EqualTo(2));
            Assert.That(queue.Stats.ConsecutiveTransportFailures, Is.EqualTo(2));
            Assert.That(gaveUp.Count, Is.EqualTo(1));
        }

        [Test]
        public void StaleRequests_AreNeverSent_AndActiveOnesAreCancelled()
        {
            var wanted = true;
            queue.Enqueue(Request("first", wanted: () => wanted), 0);
            queue.Update(0);
            Assert.That(transport.Calls.Count, Is.EqualTo(1));

            wanted = false;
            queue.Update(1);
            Assert.That(transport.Calls[0].Cancelled, Is.True);

            queue.Enqueue(Request("never", wanted: () => false), 2);
            queue.Update(2);

            Assert.That(transport.Calls.Count, Is.EqualTo(1));
            Assert.That(queue.Stats.Stale, Is.EqualTo(2));
            Assert.That(handled, Is.Empty);
        }

        [Test]
        public void StaleReply_IsDiscardedWithoutRetry()
        {
            queue.Enqueue(Request("late", handle: _ => AiReplyCheck.Stale()), 0);
            transport.Responder = call => call.Succeed("{}");
            queue.Update(0);
            queue.Update(1);
            queue.Update(10);

            Assert.That(transport.Calls.Count, Is.EqualTo(1));
            Assert.That(queue.Stats.Stale, Is.EqualTo(1));
            Assert.That(gaveUp, Is.Empty);
        }

        [Test]
        public void FullQueue_DropsTheLeastImportantRequest()
        {
            queue.Enqueue(Request("active"), 0);
            queue.Update(0);
            queue.Enqueue(Request("d1"), 0);
            queue.Enqueue(Request("flavour", AiPriority.Flavour), 0);
            queue.Enqueue(Request("d2"), 0);
            queue.Enqueue(Request("d3"), 0);
            var accepted = queue.Enqueue(Request("urgent", AiPriority.Urgent), 0);
            var rejected = queue.Enqueue(Request("flavour-2", AiPriority.Flavour), 0);

            Assert.That(accepted, Is.True);
            Assert.That(rejected, Is.False);
            Assert.That(queue.PendingCount, Is.EqualTo(4));
            Assert.That(queue.Pending.Any(p => p.Key == "flavour"), Is.False);
            Assert.That(queue.Stats.Dropped, Is.EqualTo(2));
            Assert.That(gaveUp.Count, Is.EqualTo(2));
        }

        [Test]
        public void CancelAll_TellsEveryOwner()
        {
            queue.Enqueue(Request("a"), 0);
            queue.Enqueue(Request("b"), 0);
            queue.Update(0);
            queue.CancelAll("offline");

            Assert.That(gaveUp, Is.EquivalentTo(new[] { "a: offline", "b: offline" }));
            Assert.That(queue.PendingCount, Is.EqualTo(0));
            Assert.That(transport.Calls[0].Cancelled, Is.True);
        }
    }
}
