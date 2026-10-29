using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;
using TinyCity.Sound;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Phase 6 build 3: the generated music, ambience and effects, replacement files, and which sounds the town makes.</summary>
    public class SoundTests
    {
        [Test]
        public void EverySound_IsGenerated_Clean_AndWithinLimits()
        {
            var keys = SoundLibrary.Keys.ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "keys are unique");
            foreach (var key in keys)
            {
                var sound = SoundLibrary.Generate(key);
                Assert.That(sound, Is.Not.Null, key);
                Assert.That(sound.Key, Is.EqualTo(key));
                Assert.That(sound.Samples.All(s => !float.IsNaN(s) && !float.IsInfinity(s)), Is.True, $"{key} has only real numbers");
                Assert.That(sound.Peak, Is.GreaterThan(0.1).And.LessThanOrEqualTo(0.75), $"{key} is audible but not harsh");
                Assert.That(sound.Loops, Is.EqualTo(SoundLibrary.IsLoop(key)), key);
                Assert.That(sound.Seconds, sound.Loops ? (NUnit.Framework.Constraints.IResolveConstraint)Is.GreaterThan(20) : Is.LessThan(3), key);
            }

            Assert.That(SoundLibrary.Generate("no-such-sound"), Is.Null);
        }

        [Test]
        public void Sounds_AreTheSameEveryTime()
        {
            foreach (var key in new[] { "music-day", "ambience-night", "sfx-footstep-2", "sfx-door" })
            {
                Assert.That(SoundLibrary.Generate(key).Samples, Is.EqualTo(SoundLibrary.Generate(key).Samples), key);
            }

            Assert.That(SoundLibrary.Generate("sfx-footstep-0").Samples, Is.Not.EqualTo(SoundLibrary.Generate("sfx-footstep-1").Samples), "steps vary");
            Assert.That(SoundLibrary.Generate("music-day").Samples.Length, Is.Not.EqualTo(SoundLibrary.Generate("music-night").Samples.Length), "two different pieces");
        }

        [Test]
        public void Loops_JoinWithoutAClick()
        {
            foreach (var key in SoundLibrary.Keys.Where(SoundLibrary.IsLoop))
            {
                var s = SoundLibrary.Generate(key).Samples;
                var jump = Math.Abs(s[0] - s[s.Length - 1]);
                var typical = Enumerable.Range(1, s.Length - 1).Select(i => Math.Abs(s[i] - s[i - 1])).Max();
                Assert.That(jump, Is.LessThanOrEqualTo(typical + 1e-4), $"{key}: the end runs into the start like any other two samples");
            }
        }

        [Test]
        public void Wav_RoundTrips_AndReadsOtherFormats()
        {
            var original = SoundLibrary.Generate("sfx-chime");
            var (samples, rate) = Wav.Read(Wav.Write(original.Samples, original.SampleRate));
            Assert.That(rate, Is.EqualTo(original.SampleRate));
            Assert.That(samples.Length, Is.EqualTo(original.Samples.Length));
            Assert.That(samples.Zip(original.Samples, (a, b) => Math.Abs(a - b)).Max(), Is.LessThan(1e-3));

            // Stereo 8-bit at 44.1 kHz is mixed down to mono.
            var stereo = new List<byte>();
            stereo.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            stereo.AddRange(BitConverter.GetBytes(36 + 4));
            stereo.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            stereo.AddRange(BitConverter.GetBytes(16));
            stereo.AddRange(BitConverter.GetBytes((short)1));
            stereo.AddRange(BitConverter.GetBytes((short)2));
            stereo.AddRange(BitConverter.GetBytes(44100));
            stereo.AddRange(BitConverter.GetBytes(44100 * 2));
            stereo.AddRange(BitConverter.GetBytes((short)2));
            stereo.AddRange(BitConverter.GetBytes((short)8));
            stereo.AddRange(System.Text.Encoding.ASCII.GetBytes("data"));
            stereo.AddRange(BitConverter.GetBytes(4));
            stereo.AddRange(new byte[] { 255, 128, 0, 128 });
            var (mixed, mixedRate) = Wav.Read(stereo.ToArray());
            Assert.That(mixedRate, Is.EqualTo(44100));
            Assert.That(mixed.Length, Is.EqualTo(2));
            Assert.That(mixed[0], Is.GreaterThan(0.45f));
            Assert.That(mixed[1], Is.LessThan(-0.45f));

            Assert.Throws<FormatException>(() => Wav.Read(new byte[] { 1, 2, 3 }));
            Assert.Throws<FormatException>(() => Wav.Read(System.Text.Encoding.ASCII.GetBytes("RIFF....WAVEjunk")));
        }

        [Test]
        public void ReplacementFiles_AreUsed_AndBadOnesFallBack()
        {
            var replacement = Wav.Write(new[] { 0.1f, -0.1f, 0.2f }, 16000);
            var used = SoundLibrary.Load("sfx-door", key => key == "sfx-door" ? replacement : null);
            Assert.That(used.Samples.Length, Is.EqualTo(3));
            Assert.That(used.SampleRate, Is.EqualTo(16000));
            Assert.That(used.Loops, Is.False);

            var problems = new List<string>();
            var fallback = SoundLibrary.Load("music-night", key => new byte[] { 0, 1, 2 }, problems.Add);
            Assert.That(fallback.Samples, Is.EqualTo(SoundLibrary.Generate("music-night").Samples));
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("music-night.wav"));
        }

        [Test]
        public void Director_SoundsSteps_Doors_AndActivities()
        {
            var sim = StarterTown.CreateNewGame();
            var director = new SoundDirector();
            Assert.That(director.Update(sim).Count, Is.EqualTo(0), "the first look only notes where everyone is");

            var cues = new List<SoundCue>();
            for (var minute = 0; minute < 3 * 24 * 60; minute++)
            {
                sim.Tick();
                cues.AddRange(director.Update(sim));
            }

            var effects = new HashSet<SoundEffect>(cues.Select(c => c.Effect));
            foreach (var expected in new[] { SoundEffect.Footstep, SoundEffect.Door, SoundEffect.Cooking, SoundEffect.Drink })
            {
                Assert.That(effects.Contains(expected), Is.True, $"three days in the city include {expected}");
            }

            Assert.That(cues.Where(c => c.Effect == SoundEffect.Footstep).Select(c => c.Variant).Distinct().Count(), Is.EqualTo(SoundEffects.FootstepVariants));
            foreach (var cue in cues)
            {
                Assert.That(SoundLibrary.Generate(cue.Key), Is.Not.Null, cue.Key);
                Assert.That(sim.World.TryGetMap(cue.Location.MapId, out _), Is.True);
            }
        }

        [Test]
        public void Director_IsQuietForTheDead_AndAfterAReset()
        {
            var sim = StarterTown.CreateNewGame();
            var director = new SoundDirector();
            director.Update(sim);
            var mabel = sim.GetResident(StarterTown.ResidentId);
            mabel.IsAlive = false;
            for (var i = 0; i < 600; i++)
            {
                sim.Tick();
                Assert.That(director.Update(sim).Any(c => c.ResidentId == mabel.Id), Is.False);
            }

            director.Reset();
            Assert.That(director.Update(sim).Count, Is.EqualTo(0));
        }

        [Test]
        public void Work_SoundsLikeTheWorkplace()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            r.State = ResidentState.Performing;
            r.ActivityId = ActivityCatalog.Work.Id;

            r.TargetObjectId = "obj-cafe-counter";
            Assert.That(SoundDirector.EffectFor(sim.World, r), Is.EqualTo(SoundEffect.Till));
            r.TargetObjectId = "obj-clinic-desk";
            Assert.That(SoundDirector.EffectFor(sim.World, r), Is.EqualTo(SoundEffect.Typing));
            r.TargetObjectId = sim.World.Objects.First(o => o.Type == ObjectType.GardenPlot).Id;
            Assert.That(SoundDirector.EffectFor(sim.World, r), Is.EqualTo(SoundEffect.Digging));

            r.ActivityId = ActivityCatalog.Sleep.Id;
            Assert.That(SoundDirector.EffectFor(sim.World, r), Is.Null, "sleeping is quiet");
        }
    }
}
