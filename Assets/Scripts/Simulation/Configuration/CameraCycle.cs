using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Configuration
{
    /// <summary>What the camera panels take turns showing when they cycle.</summary>
    public enum CycleMode
    {
        /// <summary>Living residents, each followed by a panel.</summary>
        Residents,

        /// <summary>Buildings: businesses and homes someone lives in.</summary>
        Buildings,

        /// <summary>Half the panels on residents and half on buildings; a single panel takes them in turn.</summary>
        HalfAndHalf,

        /// <summary>Everyone, then every building, in one list.</summary>
        Everything,
    }

    /// <summary>One thing a panel can show: a resident to follow, or a building to look into.</summary>
    public readonly struct CameraTarget : IEquatable<CameraTarget>
    {
        private CameraTarget(string residentId, string mapId)
        {
            ResidentId = residentId;
            MapId = mapId;
        }

        public string ResidentId { get; }

        public string MapId { get; }

        public bool IsResident => ResidentId != null;

        public static CameraTarget Resident(string id) => new CameraTarget(id, null);

        public static CameraTarget Place(string mapId) => new CameraTarget(null, mapId);

        public bool Equals(CameraTarget other) => ResidentId == other.ResidentId && MapId == other.MapId;

        public override bool Equals(object obj) => obj is CameraTarget other && Equals(other);

        public override int GetHashCode() => (ResidentId ?? MapId ?? string.Empty).GetHashCode();

        public override string ToString() => IsResident ? "res:" + ResidentId : "map:" + MapId;
    }

    /// <summary>
    /// Turns the camera panels over every so many game minutes. The things to show (<see cref="Targets"/>) are split
    /// between the open panels: with one panel it shows each in turn, with four it shows four at a time, the next four
    /// at the next turn, and so on, wrapping round at the end. It only does the sums; the game asks
    /// <see cref="Due"/> each frame and points its panels where <see cref="Assign"/> says. Nothing here is saved.
    /// </summary>
    public sealed class CameraCycle
    {
        /// <summary>The intervals the player can choose, in game minutes.</summary>
        public static readonly int[] Intervals = { 30, 60, 120, 180, 360, 720 };

        public const int DefaultInterval = 60;

        private long lastTurnMinute = -1;

        /// <summary>How many turns have been taken.</summary>
        public int Turn { get; private set; }

        /// <summary>Starts again from the first turn at <paramref name="nowMinute"/>.</summary>
        public void Restart(long nowMinute)
        {
            Turn = 0;
            lastTurnMinute = nowMinute;
        }

        /// <summary>
        /// True when a turn is due: the first time it is asked, or once <paramref name="intervalMinutes"/> game minutes
        /// have passed since the last one (a clock that went backwards, as after loading an older town, also starts a turn).
        /// </summary>
        public bool Due(long nowMinute, int intervalMinutes)
        {
            if (lastTurnMinute < 0 || nowMinute < lastTurnMinute)
            {
                lastTurnMinute = nowMinute;
                return true;
            }

            if (nowMinute - lastTurnMinute < Math.Max(1, intervalMinutes))
            {
                return false;
            }

            Turn++;
            lastTurnMinute = nowMinute;
            return true;
        }

        /// <summary>Moves on one turn straight away (the Next button), restarting the wait.</summary>
        public void Skip(long nowMinute)
        {
            Turn++;
            lastTurnMinute = nowMinute;
        }

        /// <summary>Game minutes until the next turn, or 0 when one is due.</summary>
        public long MinutesLeft(long nowMinute, int intervalMinutes) =>
            lastTurnMinute < 0 ? 0 : Math.Max(0, Math.Max(1, intervalMinutes) - (nowMinute - lastTurnMinute));

        /// <summary>
        /// Living residents in town order, and buildings in <paramref name="placeOrder"/>: every business, and each home
        /// a living resident has (empty homes are left out). With <paramref name="busyOnly"/>, only buildings with
        /// someone alive inside right now.
        /// </summary>
        public static (List<CameraTarget> Residents, List<CameraTarget> Buildings) Targets(TownSimulation simulation, IEnumerable<TileMap> placeOrder, bool busyOnly)
        {
            var living = simulation.Residents.Where(r => r.IsAlive).ToList();
            var residents = living.Select(r => CameraTarget.Resident(r.Id)).ToList();
            var homes = new HashSet<string>(living.Where(r => !r.IsHomeless).Select(r => r.HomeMapId));
            var occupied = new HashSet<string>(living.Select(r => r.Location.MapId));
            var buildings = placeOrder
                .Where(m => !m.IsExterior)
                .Where(m => simulation.World.BusinessOnMap(m.Id) != null || homes.Contains(m.Id))
                .Where(m => !busyOnly || occupied.Contains(m.Id))
                .Select(m => CameraTarget.Place(m.Id))
                .ToList();
            return (residents, buildings);
        }

        /// <summary>
        /// What each of <paramref name="panels"/> panels shows on turn <paramref name="turn"/>. An entry is null where a
        /// panel has nothing new to show (more panels than things to show), and it keeps what it has.
        /// </summary>
        public static CameraTarget?[] Assign(CycleMode mode, IReadOnlyList<CameraTarget> residents, IReadOnlyList<CameraTarget> buildings, int panels, int turn)
        {
            panels = Math.Max(0, panels);
            turn = Math.Max(0, turn);
            var result = new CameraTarget?[panels];
            switch (mode)
            {
                case CycleMode.Residents:
                    Fill(result, 0, panels, residents, turn);
                    break;
                case CycleMode.Buildings:
                    Fill(result, 0, panels, buildings, turn);
                    break;
                case CycleMode.Everything:
                    Fill(result, 0, panels, residents.Concat(buildings).ToList(), turn);
                    break;
                default:
                    if (panels == 1)
                    {
                        Fill(result, 0, 1, Interleave(residents, buildings), turn);
                    }
                    else
                    {
                        var forResidents = (panels + 1) / 2;
                        Fill(result, 0, forResidents, residents, turn);
                        Fill(result, forResidents, panels - forResidents, buildings, turn);
                    }

                    break;
            }

            return result;
        }

        /// <summary>A resident, a building, the next resident and so on; the longer list carries on alone.</summary>
        private static List<CameraTarget> Interleave(IReadOnlyList<CameraTarget> residents, IReadOnlyList<CameraTarget> buildings)
        {
            var both = new List<CameraTarget>();
            for (var i = 0; i < Math.Max(residents.Count, buildings.Count); i++)
            {
                if (i < residents.Count)
                {
                    both.Add(residents[i]);
                }

                if (i < buildings.Count)
                {
                    both.Add(buildings[i]);
                }
            }

            return both;
        }

        /// <summary>
        /// Gives <paramref name="count"/> panels from <paramref name="start"/> the next page of <paramref name="targets"/>:
        /// page <paramref name="turn"/> wrapping round. When there are no more things than panels, each shows one and
        /// the rest keep theirs.
        /// </summary>
        private static void Fill(CameraTarget?[] result, int start, int count, IReadOnlyList<CameraTarget> targets, int turn)
        {
            if (count <= 0 || targets.Count == 0)
            {
                return;
            }

            if (targets.Count <= count)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    result[start + i] = targets[i];
                }

                return;
            }

            var first = (int)(((long)turn * count) % targets.Count);
            for (var i = 0; i < count; i++)
            {
                result[start + i] = targets[(first + i) % targets.Count];
            }
        }
    }
}
