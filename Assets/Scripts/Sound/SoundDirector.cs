using System.Collections.Generic;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Sound
{
    /// <summary>A sound to play for something a resident did, and where it happened.</summary>
    public readonly struct SoundCue
    {
        public SoundCue(SoundEffect effect, int variant, string residentId, Location location)
        {
            Effect = effect;
            Variant = variant;
            ResidentId = residentId;
            Location = location;
        }

        public SoundEffect Effect { get; }

        public int Variant { get; }

        public string ResidentId { get; }

        public Location Location { get; }

        public string Key => SoundEffects.KeyOf(Effect, Variant);
    }

    /// <summary>
    /// Turns what happened in each minute of the simulation into sounds: a footstep for each step, a door when
    /// someone goes in or out, and a sound for the activity they start (repeated now and then while it lasts).
    /// It only reads the town, never changes it, and nothing it remembers is saved.
    /// </summary>
    public sealed class SoundDirector
    {
        /// <summary>An activity's sound comes again after this many game minutes.</summary>
        public const int RepeatMinutes = 10;

        private readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();

        private sealed class Seen
        {
            public Location Location;
            public string ActivityKey;
            public long SoundedAt;
            public int Steps;
        }

        /// <summary>Forgets everyone, as after loading a town: the next update makes no sounds, only notes where people are.</summary>
        public void Reset() => seen.Clear();

        /// <summary>The sounds for the minute just simulated.</summary>
        public List<SoundCue> Update(TownSimulation simulation)
        {
            var cues = new List<SoundCue>();
            var now = simulation.Time.TotalMinutes;
            foreach (var r in simulation.Residents)
            {
                if (!r.IsAlive)
                {
                    seen.Remove(r.Id);
                    continue;
                }

                var activityKey = r.State == ResidentState.Performing ? r.ActivityId + "@" + r.TargetObjectId : null;
                if (!seen.TryGetValue(r.Id, out var before))
                {
                    seen[r.Id] = new Seen { Location = r.Location, ActivityKey = activityKey, SoundedAt = now };
                    continue;
                }

                if (r.Location.MapId != before.Location.MapId)
                {
                    cues.Add(new SoundCue(SoundEffect.Door, 0, r.Id, r.Location));
                }
                else if (r.Location.Position != before.Location.Position)
                {
                    before.Steps++;
                    cues.Add(new SoundCue(SoundEffect.Footstep, before.Steps % SoundEffects.FootstepVariants, r.Id, r.Location));
                }

                if (activityKey != null)
                {
                    var started = activityKey != before.ActivityKey;
                    if (started || now - before.SoundedAt >= RepeatMinutes)
                    {
                        var effect = EffectFor(simulation.World, r);
                        if (effect != null)
                        {
                            cues.Add(new SoundCue(effect.Value, 0, r.Id, r.Location));
                        }

                        before.SoundedAt = now;
                    }
                }

                before.Location = r.Location;
                before.ActivityKey = activityKey;
            }

            return cues;
        }

        /// <summary>The sound of what a resident is doing, or null for quiet things like sleeping and reading.</summary>
        public static SoundEffect? EffectFor(TownWorld world, Resident r)
        {
            var id = r.ActivityId;
            if (id == ActivityCatalog.Eat.Id || id == ActivityCatalog.Snack.Id || id == ActivityCatalog.Bake.Id)
            {
                return SoundEffect.Cooking;
            }

            if (id == ActivityCatalog.Drink.Id || id == ActivityCatalog.Coffee.Id || id == ActivityCatalog.BarDrink.Id)
            {
                return SoundEffect.Drink;
            }

            if (id == ActivityCatalog.BuyGroceries.Id || id == ActivityCatalog.Pastry.Id)
            {
                return SoundEffect.Till;
            }

            if (id == ActivityCatalog.PlayArcade.Id)
            {
                return SoundEffect.Typing;
            }

            if (id == ActivityCatalog.BrowseMarket.Id)
            {
                return SoundEffect.Till;
            }

            if (id == ActivityCatalog.Chess.Id || id == ActivityCatalog.PlayPool.Id || id == ActivityCatalog.PlaySnooker.Id)
            {
                return SoundEffect.ChessMove;
            }

            if (id == ActivityCatalog.PlayPiano.Id)
            {
                return SoundEffect.Piano;
            }

            if (id == ActivityCatalog.Fish.Id)
            {
                return SoundEffect.Splash;
            }

            if (id == ActivityCatalog.Garden.Id)
            {
                return SoundEffect.Digging;
            }

            if (id == ActivityCatalog.Work.Id && r.TargetObjectId != null && world.TryGetObject(r.TargetObjectId, out var workplace))
            {
                switch (workplace.Type)
                {
                    case ObjectType.GardenPlot:
                        return SoundEffect.Digging;
                    case ObjectType.Counter:
                        // Counters where things are sold ring them up; the clinic desk is paperwork.
                        var business = world.BusinessOnMap(workplace.Location.MapId);
                        return business != null && business.Items.Count > 0 ? SoundEffect.Till : SoundEffect.Typing;
                    default:
                        return SoundEffect.Typing;
                }
            }

            return null;
        }
    }
}
