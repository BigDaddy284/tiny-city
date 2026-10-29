using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>What a resident's body is doing, which picks their animation.</summary>
    public enum ResidentAction
    {
        Stand,
        Walk,
        Talk,
        Eat,
        Read,
        Work,
        Cheer,
        Exercise,
        Sleep,
    }

    public enum Stance
    {
        Standing,
        Sitting,
        Lying,
    }

    /// <summary>
    /// Which way a figure faces. The game's four walking directions on screen: down-right (+x), down-left (+y),
    /// up-left (-x) and up-right (-y). <see cref="Away"/> means their back is to the viewer.
    /// </summary>
    public readonly struct Facing
    {
        public Facing(bool away, bool left)
        {
            Away = away;
            Left = left;
        }

        public bool Away { get; }

        public bool Left { get; }

        public static readonly Facing DownRight = new Facing(false, false);
        public static readonly Facing DownLeft = new Facing(false, true);
        public static readonly Facing UpLeft = new Facing(true, true);
        public static readonly Facing UpRight = new Facing(true, false);

        /// <summary>The facing for a step of (dx, dy) tiles, or <paramref name="otherwise"/> for no step.</summary>
        public static Facing OfStep(int dx, int dy, Facing otherwise)
        {
            if (dx > 0)
            {
                return DownRight;
            }

            if (dx < 0)
            {
                return UpLeft;
            }

            if (dy > 0)
            {
                return DownLeft;
            }

            return dy < 0 ? UpRight : otherwise;
        }

        /// <summary>Faces the way from one tile towards another (for example a partner in a chat).</summary>
        public static Facing Towards(GridPos from, GridPos to, Facing otherwise)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            if (dx == 0 && dy == 0)
            {
                return otherwise;
            }

            return System.Math.Abs(dx) >= System.Math.Abs(dy) ? OfStep(System.Math.Sign(dx), 0, otherwise) : OfStep(0, System.Math.Sign(dy), otherwise);
        }
    }

    /// <summary>Where on a tile a resident using a piece of furniture is drawn, and how they sit, stand or lie there.</summary>
    public readonly struct Placement
    {
        public Placement(double u, double v, double z, Stance stance, Facing facing, bool behind = false)
        {
            U = u;
            V = v;
            Z = z;
            Stance = stance;
            Facing = facing;
            Behind = behind;
        }

        /// <summary>Position on the tile, 0..1 along x (u) and y (v); 0.5, 0.5 is the centre.</summary>
        public double U { get; }

        public double V { get; }

        /// <summary>Height in pixels: the seat for someone sitting.</summary>
        public double Z { get; }

        public Stance Stance { get; }

        public Facing Facing { get; }

        /// <summary>A seat on the far side of the furniture: the resident is drawn behind it, so the table hides their lap.</summary>
        public bool Behind { get; }

        public static readonly Placement Centre = new Placement(0.5, 0.5, 0, Stance.Standing, Facing.DownRight);

        /// <summary>
        /// How to use each kind of furniture: seats and beds hold the resident where the picture has its seat or
        /// pillow, and worktops are used from the front, facing them. Furniture for more than one person has a place
        /// for each <paramref name="seat"/> (0 is the first to arrive), such as the two chairs of a café table; a seat
        /// past the last one shares the first.
        /// </summary>
        public static Placement For(ObjectType type, int seat = 0)
        {
            if (seat > 0)
            {
                var other = OtherPlace(type, seat);
                if (other != null)
                {
                    return other.Value;
                }
            }

            switch (type)
            {
                case ObjectType.Armchair: return new Placement(0.52, 0.5, 13, Stance.Sitting, Facing.DownRight);
                case ObjectType.Bench: return new Placement(0.32, 0.5, 12, Stance.Sitting, Facing.DownLeft);
                case ObjectType.CafeTable: return new Placement(0.5, 0.82, 12, Stance.Sitting, Facing.UpRight);
                case ObjectType.ChessTable: return new Placement(0.92, 0.5, 10, Stance.Sitting, Facing.UpLeft);
                case ObjectType.Piano: return new Placement(0.5, 0.78, 9, Stance.Sitting, Facing.UpRight);
                case ObjectType.Toilet: return new Placement(0.5, 0.55, 13, Stance.Sitting, Facing.DownLeft);
                case ObjectType.Bed:
                case ObjectType.ClinicBed:
                    return new Placement(0.5, 0.5, 0, Stance.Lying, Facing.DownRight);
                case ObjectType.Kitchen:
                case ObjectType.Sink:
                case ObjectType.Counter:
                case ObjectType.Workbench:
                case ObjectType.ShopShelf:
                case ObjectType.BakeryDisplay:
                case ObjectType.Easel:
                case ObjectType.GardenTap:
                    return new Placement(0.5, 1.0, 0, Stance.Standing, Facing.UpRight);
                case ObjectType.FishingSpot: return new Placement(0.7, 0.5, 4, Stance.Standing, Facing.DownRight);
                case ObjectType.ExerciseSpot: return new Placement(0.5, 0.5, 1, Stance.Standing, Facing.DownRight);
                default: return Centre;
            }
        }

        /// <summary>The places after the first, or null where there are no more.</summary>
        private static Placement? OtherPlace(ObjectType type, int seat)
        {
            switch (type)
            {
                case ObjectType.Bench when seat == 1: return new Placement(0.68, 0.5, 12, Stance.Sitting, Facing.DownLeft);
                case ObjectType.CafeTable when seat == 1: return new Placement(0.5, 0.14, 12, Stance.Sitting, Facing.DownLeft, behind: true);
                case ObjectType.ChessTable when seat == 1: return new Placement(0.06, 0.5, 10, Stance.Sitting, Facing.DownRight, behind: true);
                case ObjectType.FishingSpot when seat == 1: return new Placement(0.7, 0.26, 4, Stance.Standing, Facing.DownRight);
                case ObjectType.ExerciseSpot when seat == 1: return new Placement(0.28, 0.66, 1, Stance.Standing, Facing.DownRight);
                case ObjectType.Counter:
                case ObjectType.Workbench:
                case ObjectType.ShopShelf:
                case ObjectType.BakeryDisplay:
                    return seat <= 2 ? new Placement(seat == 1 ? 0.2 : 0.8, 1.0, 0, Stance.Standing, Facing.UpRight) : (Placement?)null;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Who sits where. Each resident using a piece of furniture keeps the first free place they found on arrival
    /// until they leave it, so two people at a café table take a chair each instead of sitting on one another.
    /// This is only for drawing and is never saved.
    /// </summary>
    public sealed class Seating
    {
        private readonly Dictionary<string, (string objectId, int seat)> places = new Dictionary<string, (string objectId, int seat)>();

        /// <summary>Brings the places up to date: frees those of residents who have left and seats new arrivals.</summary>
        public void Update(TownWorld world, IReadOnlyList<Resident> residents)
        {
            var inUse = new Dictionary<string, string>();
            foreach (var r in residents)
            {
                if (r.IsAlive && r.State == ResidentState.Performing && r.TargetObjectId != null
                    && world.TryGetObject(r.TargetObjectId, out var obj) && obj.Location == r.Location)
                {
                    inUse[r.Id] = obj.Id;
                }
            }

            foreach (var id in places.Keys.ToList())
            {
                if (!inUse.TryGetValue(id, out var objectId) || objectId != places[id].objectId)
                {
                    places.Remove(id);
                }
            }

            foreach (var r in residents)
            {
                if (!inUse.TryGetValue(r.Id, out var objectId) || places.ContainsKey(r.Id))
                {
                    continue;
                }

                var taken = new HashSet<int>(places.Values.Where(p => p.objectId == objectId).Select(p => p.seat));
                var seat = 0;
                while (taken.Contains(seat))
                {
                    seat++;
                }

                places[r.Id] = (objectId, seat);
            }
        }

        /// <summary>The resident's place at the furniture they are using, 0 if none.</summary>
        public int SeatOf(string residentId) => places.TryGetValue(residentId, out var place) ? place.seat : 0;

        public void Clear() => places.Clear();
    }

    /// <summary>One frame of a resident's animation.</summary>
    public readonly struct ResidentPose
    {
        public ResidentPose(ResidentAction action, Stance stance, Facing facing, int frame)
        {
            Action = action;
            Stance = action == ResidentAction.Sleep ? Stance.Lying : stance == Stance.Lying ? Stance.Sitting : stance;
            Facing = facing;
            Frame = ((frame % FramesOf(action)) + FramesOf(action)) % FramesOf(action);
        }

        public ResidentAction Action { get; }

        public Stance Stance { get; }

        public Facing Facing { get; }

        public int Frame { get; }

        /// <summary>A short name for caching the picture.</summary>
        public string Key => $"{Action}-{Stance}-{(Facing.Away ? "away" : "front")}-{(Facing.Left ? "l" : "r")}-{Frame}";

        public static int FramesOf(ResidentAction action)
        {
            switch (action)
            {
                case ResidentAction.Walk: return 4;
                case ResidentAction.Stand:
                case ResidentAction.Sleep:
                    return 1;
                default: return 2;
            }
        }

        /// <summary>
        /// What the resident is doing, read from the simulation: walking while travelling, talking in a chat, and
        /// otherwise the activity they are performing with the furniture they are using.
        /// </summary>
        public static (ResidentAction action, Placement placement) Choose(TownWorld world, Resident resident, bool moving, int seat = 0)
        {
            if (resident.State == ResidentState.Travelling && moving)
            {
                return (ResidentAction.Walk, Placement.Centre);
            }

            if (resident.State == ResidentState.Talking)
            {
                return (ResidentAction.Talk, Placement.Centre);
            }

            if (resident.State != ResidentState.Performing)
            {
                return (ResidentAction.Stand, Placement.Centre);
            }

            var placement = Placement.Centre;
            if (resident.TargetObjectId != null && world.TryGetObject(resident.TargetObjectId, out var obj) && obj.Location == resident.Location)
            {
                placement = Placement.For(obj.Type, seat);
            }

            return (ActionFor(resident.ActivityId, placement.Stance), placement);
        }

        private static ResidentAction ActionFor(string activityId, Stance stance)
        {
            if (activityId == ActivityCatalog.Sleep.Id || activityId == ActivityCatalog.Rest.Id)
            {
                return stance == Stance.Lying ? ResidentAction.Sleep : ResidentAction.Stand;
            }

            if (activityId == ActivityCatalog.Eat.Id || activityId == ActivityCatalog.Snack.Id || activityId == ActivityCatalog.CafeMeal.Id
                || activityId == ActivityCatalog.Pastry.Id || activityId == ActivityCatalog.Drink.Id || activityId == ActivityCatalog.Coffee.Id)
            {
                return ResidentAction.Eat;
            }

            if (activityId == ActivityCatalog.Read.Id || activityId == ActivityCatalog.Knit.Id)
            {
                return ResidentAction.Read;
            }

            if (activityId == ActivityCatalog.Exercise.Id)
            {
                return ResidentAction.Exercise;
            }

            if (activityId == ActivityCatalog.AttendEvent.Id)
            {
                return ResidentAction.Cheer;
            }

            if (activityId == ActivityCatalog.Chat.Id)
            {
                return ResidentAction.Talk;
            }

            if (activityId == ActivityCatalog.Work.Id || activityId == ActivityCatalog.Paint.Id || activityId == ActivityCatalog.PlayPiano.Id
                || activityId == ActivityCatalog.Chess.Id || activityId == ActivityCatalog.Fish.Id || activityId == ActivityCatalog.Garden.Id
                || activityId == ActivityCatalog.Bake.Id || activityId == ActivityCatalog.BuyGroceries.Id)
            {
                return ResidentAction.Work;
            }

            return ResidentAction.Stand;
        }
    }
}
