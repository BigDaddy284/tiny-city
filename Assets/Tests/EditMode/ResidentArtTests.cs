using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Art;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;

namespace TinyTown.Tests.EditMode
{
    /// <summary>Phase 6, build 2: the residents' looks, animations and mood icons.</summary>
    public class ResidentArtTests
    {
        private static readonly Facing[] Facings = { Facing.DownRight, Facing.DownLeft, Facing.UpLeft, Facing.UpRight };

        /// <summary>Every frame of every action, in every stance and facing it can be shown in.</summary>
        public static IEnumerable<ResidentPose> AllPoses()
        {
            foreach (ResidentAction action in Enum.GetValues(typeof(ResidentAction)))
            {
                foreach (Stance stance in Enum.GetValues(typeof(Stance)))
                {
                    foreach (var facing in Facings)
                    {
                        for (var f = 0; f < ResidentPose.FramesOf(action); f++)
                        {
                            yield return new ResidentPose(action, stance, facing, f);
                        }
                    }
                }
            }
        }

        [Test]
        public void TenStarterResidents_AllLookDifferent()
        {
            var world = StarterTown.CreateWorld();
            var looks = StarterTown.CreateResidents(world).Select(r => ResidentLook.For(r.Id, r.AgeYears)).ToList();
            Assert.That(looks.Count, Is.EqualTo(10));
            Assert.That(looks.Select(l => l.Top).Distinct().Count(), Is.EqualTo(10), "everyone wears their own colour");
            Assert.That(looks.Select(l => (l.Hair, l.Style)).Distinct().Count(), Is.EqualTo(10), "and has their own hair");

            var pose = new ResidentPose(ResidentAction.Stand, Stance.Standing, Facing.DownRight, 0);
            var pictures = looks.Select(l => Convert.ToBase64String(ResidentArt.Render(l, pose).ToBytesBottomUp())).ToList();
            Assert.That(pictures.Distinct().Count(), Is.EqualTo(10));
        }

        [Test]
        public void NewcomersGetASteadyLook_GreyingWithAge()
        {
            var young = ResidentLook.For("res-0042", 30);
            Assert.That(ResidentLook.For("res-0042", 30).Top, Is.EqualTo(young.Top), "the same every time");
            Assert.That(ResidentLook.For("res-0042", 80).Hair, Is.EqualTo(Palette.HairWhite));
            Assert.That(ResidentLook.For("res-0042", 68).Hair, Is.EqualTo(Palette.HairGrey));
            Assert.That(ResidentLook.For(null, 30), Is.Not.Null);
        }

        [Test]
        public void EveryPose_FitsItsCanvas_WithAnOutline()
        {
            var world = StarterTown.CreateWorld();
            foreach (var resident in StarterTown.CreateResidents(world))
            {
                var look = ResidentLook.For(resident.Id, resident.AgeYears);
                foreach (var pose in AllPoses())
                {
                    var c = ResidentArt.Render(look, pose);
                    var name = $"{resident.FirstName} {pose.Key}";
                    var filled = 0;
                    var outlined = false;
                    for (var y = 0; y < c.Height; y++)
                    {
                        for (var x = 0; x < c.Width; x++)
                        {
                            var p = c.Get(x, y);
                            if (p.IsClear || p.A < 255)
                            {
                                continue;
                            }

                            filled++;
                            Assert.That(x > 0 && y > 0 && x < c.Width - 1 && y < c.Height - 1, Is.True, $"{name} is cut off at ({x}, {y})");
                            outlined |= c.Get(x - 1, y).IsClear && p.R < 140;
                        }
                    }

                    Assert.That(filled, Is.GreaterThan(pose.Stance == Stance.Lying ? 30 : 150), name);
                    Assert.That(outlined, Is.True, $"{name} has a dark outline");
                    Assert.That(ResidentArt.HeadTop(look, pose), Is.InRange(15, 40));
                }
            }
        }

        [Test]
        public void Animations_Move_AndFacingsDiffer()
        {
            var look = ResidentLook.For(StarterTown.ResidentId, 34);
            foreach (ResidentAction action in Enum.GetValues(typeof(ResidentAction)))
            {
                var frames = Enumerable.Range(0, ResidentPose.FramesOf(action))
                    .Select(f => Convert.ToBase64String(ResidentArt.Render(look, new ResidentPose(action, Stance.Standing, Facing.DownRight, f)).ToBytesBottomUp()))
                    .ToList();
                Assert.That(frames.Distinct().Count(), Is.EqualTo(frames.Count), $"each frame of {action} is different");
            }

            var faces = Facings.Select(f => Convert.ToBase64String(ResidentArt.Render(look, new ResidentPose(ResidentAction.Walk, Stance.Standing, f, 0)).ToBytesBottomUp())).ToList();
            Assert.That(faces.Distinct().Count(), Is.EqualTo(4), "four ways to face");
            Assert.That(new ResidentPose(ResidentAction.Walk, Stance.Standing, Facing.DownRight, 5).Frame, Is.EqualTo(1), "frames wrap round");
            Assert.That(new ResidentPose(ResidentAction.Sleep, Stance.Standing, Facing.DownRight, 0).Stance, Is.EqualTo(Stance.Lying), "sleeping is lying down");
        }

        [Test]
        public void Facing_FollowsTheStep()
        {
            Assert.That(Facing.OfStep(1, 0, Facing.UpLeft).Equals(Facing.DownRight), Is.True);
            Assert.That(Facing.OfStep(0, 1, Facing.UpLeft).Equals(Facing.DownLeft), Is.True);
            Assert.That(Facing.OfStep(-1, 0, Facing.DownRight).Equals(Facing.UpLeft), Is.True);
            Assert.That(Facing.OfStep(0, -1, Facing.DownRight).Equals(Facing.UpRight), Is.True);
            Assert.That(Facing.OfStep(0, 0, Facing.UpLeft).Equals(Facing.UpLeft), Is.True, "standing still keeps the last facing");
            Assert.That(Facing.Towards(new GridPos(2, 2), new GridPos(2, 5), Facing.UpLeft).Equals(Facing.DownLeft), Is.True);
        }

        [Test]
        public void EveryMood_HasItsOwnIcon()
        {
            var shown = Enum.GetValues(typeof(Mood)).Cast<Mood>().Where(m => m != Mood.None).ToList();
            Assert.That(MoodArt.Shown.OrderBy(m => m).SequenceEqual(shown), Is.True, "an icon for every mood");
            Assert.That(MoodArt.Icon(Mood.None), Is.Null);
            var icons = shown.Select(m => Convert.ToBase64String(MoodArt.Icon(m).ToBytesBottomUp())).ToList();
            Assert.That(icons.Distinct().Count(), Is.EqualTo(icons.Count));
        }

        [Test]
        public void FurnitureIsUsedSensibly()
        {
            Assert.That(Placement.For(ObjectType.Bed).Stance, Is.EqualTo(Stance.Lying));
            Assert.That(Placement.For(ObjectType.ClinicBed).Stance, Is.EqualTo(Stance.Lying));
            foreach (var seat in new[] { ObjectType.Armchair, ObjectType.Bench, ObjectType.CafeTable, ObjectType.ChessTable, ObjectType.Piano, ObjectType.Toilet })
            {
                Assert.That(Placement.For(seat).Stance, Is.EqualTo(Stance.Sitting), seat.ToString());
                Assert.That(Placement.For(seat).Z, Is.GreaterThan(5), seat.ToString());
            }

            foreach (ObjectType type in Enum.GetValues(typeof(ObjectType)))
            {
                var p = Placement.For(type);
                Assert.That(p.U >= 0 && p.U <= 1 && p.V >= 0 && p.V <= 1, Is.True, $"{type} keeps them on its tile");
            }
        }

        [Test]
        public void ADayInTheVillage_ShowsEveryEverydayAnimation()
        {
            var sim = StarterTown.CreateNewGame();
            var seen = new HashSet<ResidentAction>();
            var seenSleepingInBed = false;
            var seenSitting = false;
            var last = sim.Residents.ToDictionary(r => r.Id, r => r.Location);
            for (var minute = 0; minute < 36 * 60; minute++)
            {
                sim.Tick();
                foreach (var r in sim.Residents)
                {
                    var (action, placement) = ResidentPose.Choose(sim.World, r, last[r.Id] != r.Location);
                    last[r.Id] = r.Location;
                    seen.Add(action);
                    if (r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.Sleep.Id)
                    {
                        Assert.That(action, Is.EqualTo(ResidentAction.Sleep), $"{r.FirstName} sleeps lying down");
                        seenSleepingInBed |= placement.Stance == Stance.Lying;
                    }

                    seenSitting |= placement.Stance == Stance.Sitting;
                    if (r.State == ResidentState.Talking)
                    {
                        Assert.That(action, Is.EqualTo(ResidentAction.Talk));
                    }
                }
            }

            foreach (var everyday in new[] { ResidentAction.Walk, ResidentAction.Talk, ResidentAction.Eat, ResidentAction.Work, ResidentAction.Sleep })
            {
                Assert.That(seen.Contains(everyday), Is.True, $"{everyday} is seen in a day and a half");
            }

            Assert.That(seenSleepingInBed, Is.True);
            Assert.That(seenSitting, Is.True);
        }
    }
}
