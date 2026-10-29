using System;
using System.Collections.Generic;
using TinyCity.Simulation.AI;

namespace TinyCity.Tests.EditMode
{
    /// <summary>A scripted stand-in for Ollama. Tests decide how each call is answered and when.</summary>
    internal sealed class FakeAiTransport : IAiTransport
    {
        public const string Model = "qwen2.5:7b";

        public List<FakeAiCall> Calls { get; } = new List<FakeAiCall>();

        /// <summary>Answers each new call; return null to leave it pending until the test completes it.</summary>
        public Func<FakeAiCall, FakeAiCall> Responder { get; set; }

        public int Throws { get; set; }

        public static FakeAiTransport WorkingOllama(Func<string, string> chatContent)
        {
            return new FakeAiTransport
            {
                Responder = call =>
                {
                    if (call.Url.EndsWith("/api/tags"))
                    {
                        return call.Succeed("{\"models\":[{\"name\":\"" + Model + "\"},{\"name\":\"llama3:latest\"}]}");
                    }

                    if (call.Body.Contains("\"messages\": []"))
                    {
                        return call.Succeed("{\"model\":\"" + Model + "\",\"done\":true}");
                    }

                    var content = chatContent(call.Body);
                    return content == null ? null : call.Succeed(ChatReply(content));
                },
            };
        }

        public static string ChatReply(string content) =>
            "{\"model\":\"" + Model + "\",\"message\":{\"role\":\"assistant\",\"content\":" + Quote(content) + "},\"done\":true}";

        public static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        public IAiCall Get(string url, double timeoutSeconds) => Start(url, null);

        public IAiCall Post(string url, string jsonBody, double timeoutSeconds) => Start(url, jsonBody);

        private IAiCall Start(string url, string body)
        {
            if (Throws > 0)
            {
                Throws--;
                throw new InvalidOperationException("network stack exploded");
            }

            var call = new FakeAiCall(url, body ?? string.Empty);
            Calls.Add(call);
            Responder?.Invoke(call);
            return call;
        }
    }

    internal sealed class FakeAiCall : IAiCall
    {
        public FakeAiCall(string url, string body)
        {
            Url = url;
            Body = body;
        }

        public string Url { get; }

        public string Body { get; }

        public bool IsDone { get; private set; }

        public bool Succeeded { get; private set; }

        public string ResponseText { get; private set; }

        public string Error { get; private set; }

        public bool Cancelled { get; private set; }

        public FakeAiCall Succeed(string text)
        {
            IsDone = true;
            Succeeded = true;
            ResponseText = text;
            return this;
        }

        public FakeAiCall Fail(string error, string text = null)
        {
            IsDone = true;
            Succeeded = false;
            Error = error;
            ResponseText = text;
            return this;
        }

        public void Cancel()
        {
            Cancelled = true;
            Fail("cancelled");
        }
    }
}
