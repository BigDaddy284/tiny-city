using TinyTown.Simulation;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;

namespace TinyTown.Art
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
        public Placement(double u, double v, double z, Stance stance, Facing facing)
        {
            U = u;
            V = v;
            Z = z;
            Stance = stance;
            Facing = facing;
        }

        /// <summary>Position on the tile, 0..1 along x (u) and y (v); 0.5, 0.5 is the centre.</summary>
        public double U { get; }

        public double V { get; }

        /// <summary>Height in pixels: the seat for someone sitting.</summary>
        public double Z { get; }

        public Stance Stance { get; }

        public Facing Facing { get; }

        public static readonly Placement Centre = new Placement(0.5, 0.5, 0, Stance.Standing, Facing.DownRight);

        /// <summary>
        /// How to use each kind of furniture: seats and beds hold the resident where the picture has its seat or
        /// pillow, and worktops are used from the front, facing them.
        /// </summary>
        public static Placement For(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Armchair: return new Placement(0.52, 0.5, 13, Stance.Sitting, Facing.DownRight);
                case ObjectType.Bench: return new Placement(0.5, 0.5, 12, Stance.Sitting, Facing.DownLeft);
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
        public static (ResidentAction action, Placement placement) Choose(TownWorld world, Resident resident, bool moving)
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
                placement = Placement.For(obj.Type);
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
