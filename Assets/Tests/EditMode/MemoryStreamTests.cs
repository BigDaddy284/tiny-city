using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's memory stream: AI importance ratings, embeddings and recall by meaning.</summary>
    public class MemoryStreamTests
    {
        private const string Mabel = "res-0001";

        [Test]
        public void Recall_WithAFocus_PrefersMemoriesCloseInMeaning()
        {
            var store = new MemoryStore();
            var garden = store.Add(Mabel, new GameTime(100), MemoryType.Event, 3, "The tomatoes are nearly ripe");
            var quarrel = store.Add(Mabel, new GameTime(100), MemoryType.Event, 3, "Argued with Tom about the rent");
            store.SetEmbedding(garden.Id, new[] { 1f, 0f });
            store.SetEmbedding(quarrel.Id, new[] { 0f, 1f });

            var aboutTom = store.Recall(Mabel, new GameTime(200), null, 1, new[] { 0.1f, 0.9f });
            var aboutGarden = store.Recall(Mabel, new GameTime(200), null, 1, new[] { 0.9f, 0.1f });

            Assert.That(aboutTom.Single().Id, Is.EqualTo(quarrel.Id));
            Assert.That(aboutGarden.Single().Id, Is.EqualTo(garden.Id));
        }

        [Test]
        public void Recall_WithoutAFocus_IsUnchangedByEmbeddings()
        {
            var plain = new MemoryStore();
            var embedded = new MemoryStore();
            foreach (var store in new[] { plain, embedded })
            {
                store.Add(Mabel, new GameTime(10), MemoryType.Event, 2, "Watered the garden", tags: new[] { "garden" });
                store.Add(Mabel, new GameTime(500), MemoryType.Event, 6, "Felix's birthday party");
                store.Add(Mabel, new GameTime(900), MemoryType.Event, 1, "Ate toast");
            }

            foreach (var m in embedded.All)
            {
                embedded.SetEmbedding(m.Id, new[] { 1f, 2f, 3f });
            }

            var a = plain.Recall(Mabel, new GameTime(1000), new[] { "garden" }, 3).Select(m => m.Id);
            var b = embedded.Recall(Mabel, new GameTime(1000), new[] { "garden" }, 3).Select(m => m.Id);
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void SetEmbedding_RejectsEmptyAndBrokenVectors()
        {
            var store = new MemoryStore();
            var m = store.Add(Mabel, new GameTime(0), MemoryType.Event, 2, "A walk");

            Assert.That(store.SetEmbedding(m.Id, new float[0]), Is.False);
            Assert.That(store.SetEmbedding(m.Id, new[] { 1f, float.NaN }), Is.False);
            Assert.That(store.SetEmbedding("mem-999999", new[] { 1f }), Is.False);
            Assert.That(m.Embedding, Is.Null);
            Assert.That(store.SetEmbedding(m.Id, new[] { 0.5f, 0.5f }), Is.True);
            Assert.That(store.FocusOf(Mabel), Is.EqualTo(new[] { 0.5f, 0.5f }));
        }

        [Test]
        public void Similarity_IsCosine_AndZeroForMismatches()
        {
            Assert.That(MemoryStore.Similarity(new[] { 1f, 0f }, new[] { 2f, 0f }), Is.EqualTo(1).Within(1e-9));
            Assert.That(MemoryStore.Similarity(new[] { 1f, 0f }, new[] { 0f, 3f }), Is.EqualTo(0).Within(1e-9));
            Assert.That(MemoryStore.Similarity(new[] { 1f }, new[] { 1f, 1f }), Is.EqualTo(0));
            Assert.That(MemoryStore.Similarity(new[] { 0f, 0f }, new[] { 1f, 1f }), Is.EqualTo(0));
            Assert.That(MemoryStore.Similarity(null, new[] { 1f }), Is.EqualTo(0));
        }

        [Test]
        public void RateImportance_ClampsAndMarksRated()
        {
            var store = new MemoryStore();
            var m = store.Add(Mabel, new GameTime(0), MemoryType.Event, 2, "Met Tom");

            Assert.That(store.NeedingRating(Mabel).Select(x => x.Id), Is.EqualTo(new[] { m.Id }));
            Assert.That(store.RateImportance(m.Id, 42), Is.True);
            Assert.That(m.Importance, Is.EqualTo(MemoryRecord.MaxImportance));
            Assert.That(m.ImportanceRated, Is.True);
            Assert.That(store.NeedingRating(Mabel).Any(), Is.False);
        }

        [Test]
        public void ImportancePrompt_KeepsOnlyAskedIdsWithValidNumbers()
        {
            const string content = "{\"ratings\":[{\"id\":\"mem-000001\",\"importance\":7},{\"id\":\"mem-000001\",\"importance\":2}," +
                "{\"id\":\"mem-000009\",\"importance\":5},{\"id\":\"mem-000002\",\"importance\":11},{\"id\":\"mem-000003\",\"importance\":\"high\"}]}";

            Assert.That(ImportancePrompt.TryParse(content, new[] { "mem-000001", "mem-000002", "mem-000003" }, out var ratings, out _), Is.True);
            Assert.That(ratings, Is.EqualTo(new Dictionary<string, int> { ["mem-000001"] = 7 }));
            Assert.That(ImportancePrompt.TryParse("{\"ratings\":[]}", new[] { "mem-000001" }, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(ImportancePrompt.TryParse("not json", new[] { "mem-000001" }, out _, out _), Is.False);
        }

        [TestCase("{\"embeddings\":[[0.1,0.2],[0.3,0.4]]}", true)]
        [TestCase("{\"embeddings\":[[0.1,0.2]]}", false)]
        [TestCase("{\"embeddings\":[[0.1,0.2],[0.3]]}", false)]
        [TestCase("{\"embeddings\":[[0.1,0.2],[]]}", false)]
        [TestCase("{\"embeddings\":[[0.1,\"x\"],[0.3,0.4]]}", false)]
        [TestCase("{\"error\":\"model not found\"}", false)]
        [TestCase("", false)]
        public void TryReadEmbeddings_ChecksCountsLengthsAndNumbers(string json, bool ok)
        {
            Assert.That(OllamaProtocol.TryReadEmbeddings(json, 2, out var vectors, out _), Is.EqualTo(ok));
            Assert.That(vectors.Count, Is.EqualTo(ok ? 2 : 0));
        }

        [Test]
        public void EmbedBody_AsksTheEmbeddingModelForEveryText()
        {
            var body = OllamaProtocol.BuildEmbedBody("nomic-embed-text", new[] { "one", "two" });
            var root = (Dictionary<string, object>)Json.Parse(body);

            Assert.That(root["model"], Is.EqualTo("nomic-embed-text"));
            Assert.That(((List<object>)root["input"]).Cast<string>().ToList(), Is.EqualTo(new List<string> { "one", "two" }));
            Assert.That(OllamaProtocol.EmbedUrl("http://localhost:11434/"), Is.EqualTo("http://localhost:11434/api/embed"));
        }

        [Test]
        public void Saves_KeepRatings_ButMakeEmbeddingsAgain()
        {
            var sim = StarterTown.CreateNewGame();
            var first = sim.Memories.All.First();
            sim.Memories.RateImportance(first.Id, 9);
            sim.Memories.SetEmbedding(first.Id, new[] { 1f, 2f });

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());

            Assert.That(loaded.Success, Is.True);
            var again = loaded.Simulation.Memories.Get(first.Id);
            Assert.That(again.Importance, Is.EqualTo(9));
            Assert.That(again.ImportanceRated, Is.True);
            Assert.That(again.Embedding, Is.Null);
            Assert.That(loaded.Simulation.Memories.All.Where(m => m.Id != first.Id).All(m => !m.ImportanceRated), Is.True);
        }

        [Test]
        public void TinyCityOneSave_IsRefused_WithAMessage()
        {
            var json = SaveSerializer.Save(StarterTown.CreateNewGame()).Replace(SaveSerializer.Format, SaveSerializer.FormatTinyCityOne);

            var result = SaveSerializer.Load(json, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.False);
            Assert.That(string.Join(" ", result.Errors), Does.Contain("Tiny City (v1)"));
        }

        [Test]
        public void AiService_RatesAndEmbedsNewMemories_InTheBackground()
        {
            var sim = StarterTown.CreateNewGame();
            var transport = new FakeAiTransport
            {
                Responder = call =>
                {
                    if (call.Url.EndsWith("/api/tags"))
                    {
                        return call.Succeed("{\"models\":[{\"name\":\"" + FakeAiTransport.Model + "\"},{\"name\":\"nomic-embed-text:latest\"}]}");
                    }

                    if (call.Body.Contains("\"messages\": []"))
                    {
                        return call.Succeed("{\"done\":true}");
                    }

                    if (call.Url.EndsWith("/api/embed"))
                    {
                        var count = ((List<object>)((Dictionary<string, object>)Json.Parse(call.Body))["input"]).Count;
                        return call.Succeed("{\"embeddings\":[" + string.Join(",", Enumerable.Repeat("[0.5,0.25]", count)) + "]}");
                    }

                    if (call.Body.Contains("ratings"))
                    {
                        var ids = System.Text.RegularExpressions.Regex.Matches(call.Body, "mem-\\d{6}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct();
                        return call.Succeed(FakeAiTransport.ChatReply("{\"ratings\":[" + string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"importance\":4}")) + "]}"));
                    }

                    return null;
                },
            };

            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            var now = 0.0;
            for (var i = 0; i < 400 && (sim.Memories.All.Any(m => !m.ImportanceRated) || sim.Memories.All.Any(m => m.Embedding == null)); i++)
            {
                ai.Update(now);
                now += 0.5;
            }

            Assert.That(ai.EmbeddingsAvailable, Is.True);
            Assert.That(sim.Memories.All.All(m => m.ImportanceRated && m.Importance == 4), Is.True);
            Assert.That(sim.Memories.All.All(m => m.Embedding != null), Is.True);
            Assert.That(transport.Calls.Count(c => c.Url.EndsWith("/api/embed")), Is.LessThanOrEqualTo((sim.Memories.All.Count + AiService.EmbedBatch - 1) / AiService.EmbedBatch));
        }

        [Test]
        public void AiService_WithoutTheEmbeddingModel_StillRatesButNeverEmbeds()
        {
            var sim = StarterTown.CreateNewGame();
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("ratings") ? "{\"ratings\":[]}" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 200; i++)
            {
                ai.Update(i * 0.5);
            }

            Assert.That(ai.EmbeddingsAvailable, Is.False);
            Assert.That(transport.Calls.Any(c => c.Url.EndsWith("/api/embed")), Is.False);

            // Ratings that never come back usable keep the game's figure instead of being asked for forever.
            Assert.That(sim.Memories.All.All(m => m.ImportanceRated), Is.True);
        }
    
        [Test]
        public void TinyCityTwo_RatesNewMemoriesOnlyInBatches()
        {
            var sim = StarterTown.CreateNewGame();
            sim.AiRunsEverything = true;
            foreach (var m in sim.Memories.All.Where(m => !m.ImportanceRated).ToList())
            {
                sim.Memories.RateImportance(m.Id, m.Importance);
            }

            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("ratings") ? "{\"ratings\":[]}" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            var id = sim.Residents[0].Id;
            var now = 0.0;
            void Run()
            {
                for (var i = 0; i < 40; i++)
                {
                    ai.Update(now);
                    now += 0.5;
                }
            }

            for (var i = 1; i < AiService.RatingBatchMin; i++)
            {
                sim.Memories.Add(id, sim.Time, MemoryType.Event, 3, "Something small happened " + i, new string[0], new string[0]);
            }

            Run();
            Assert.That(transport.Calls.Any(c => c.Body != null && c.Body.Contains("Rate how much each one matters")), Is.False, "too few to be worth a request yet");

            sim.Memories.Add(id, sim.Time, MemoryType.Event, 3, "One more thing happened", new string[0], new string[0]);
            Run();
            Assert.That(transport.Calls.Any(c => c.Body != null && c.Body.Contains("Rate how much each one matters")), Is.True);
        }
    }
}
