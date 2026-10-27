using System;
using TinyTown.Simulation.Residents;

namespace TinyTown.Simulation.Health
{
    /// <summary>
    /// Every number about health, illness, ageing and death. Deterministic: luck comes from <see cref="Chance"/>, so a
    /// town replays identically. The AI never changes health and can never declare anyone dead.
    /// </summary>
    public static class HealthRules
    {
        public const double MaxHealth = 100;

        /// <summary>Game days in one year of a resident's life: lifespans are compressed so life events happen within a playthrough.</summary>
        public const int DaysPerYear = 28;

        /// <summary>Below this level a need counts as neglected and wears health down.</summary>
        public const double NeglectedNeed = 10;

        public const double NeglectDrainPerHour = 1.5;

        public const double RecoveryPerHour = 0.5;

        public const double RestingRecoveryPerHour = 1.5;

        /// <summary>When flu meets health this poor, it turns into pneumonia.</summary>
        public const double FrailHealth = 30;

        /// <summary>Illness and old age are checked once a day at these times.</summary>
        public static readonly int IllnessCheckMinute = 5 * 60;

        public static readonly int OldAgeCheckMinute = 4 * 60;

        /// <summary>From this age, old age can end a life.</summary>
        public const int OldAgeFrom = 80;

        /// <summary>Each year past <see cref="OldAgeFrom"/> adds this much to the chance of dying within the year.</summary>
        public const double OldAgeChancePerYear = 0.08;

        /// <summary>Chatting with someone with a contagious illness raises the chance of catching something for this many days.</summary>
        public const int ExposureDays = 2;

        private static readonly NeedType[] VitalNeeds = { NeedType.Hunger, NeedType.Thirst, NeedType.Energy, NeedType.Hygiene };

        /// <summary>Older bodies don't bounce back all the way.</summary>
        public static double MaxHealthFor(int age) => age < 60 ? MaxHealth : Math.Max(40, MaxHealth - ((age - 60) * 1.5));

        public static double StartingHealthFor(int age) => Math.Min(MaxHealthFor(age), 90);

        /// <summary>The day (1–<see cref="DaysPerYear"/>) of each year that is this resident's birthday.</summary>
        public static int BirthdayCycleDay(Resident r) => 1 + ((r.BirthdayDayOfYear - 1) * DaysPerYear / 365);

        public static int CycleDay(int day) => ((day - 1) % DaysPerYear) + 1;

        public static bool IsBirthday(Resident r, int day) => CycleDay(day) == BirthdayCycleDay(r);

        public static bool CanWork(Resident r) => IllnessCatalog.Get(r.IllnessId)?.CanWork ?? true;

        public static bool KeepsToThemselves(Resident r) => IllnessCatalog.Get(r.IllnessId)?.KeepsToThemselves ?? false;

        public static bool NeedsDoctor(Resident r) => IllnessCatalog.Get(r.IllnessId)?.Serious == true && !r.IllnessTreated;

        /// <summary>
        /// One minute of health: neglected needs wear it down, illness may too, and otherwise it slowly recovers
        /// (faster while resting). Illness recovery also moves on. Returns what changed, if anything worth telling.
        /// </summary>
        public static HealthEvent ApplyMinute(Resident r, bool resting)
        {
            var illness = IllnessCatalog.Get(r.IllnessId);
            var neglected = 0;
            foreach (var need in VitalNeeds)
            {
                if (r.Needs[need] < NeglectedNeed)
                {
                    neglected++;
                }
            }

            var change = -neglected * NeglectDrainPerHour / 60.0;
            var improving = illness == null || (illness.Serious ? r.IllnessTreated : true);
            if (illness != null && !(illness.Serious && r.IllnessTreated))
            {
                change -= illness.HealthDrainPerHour / 60.0;
            }

            if (neglected == 0 && (illness == null || r.IllnessTreated))
            {
                change += (resting ? RestingRecoveryPerHour : RecoveryPerHour) / 60.0;
            }

            r.Health = Math.Max(0, Math.Min(MaxHealthFor(r.AgeYears), r.Health + change));
            if (r.Health <= 0)
            {
                return HealthEvent.Died;
            }

            if (illness == null)
            {
                return HealthEvent.None;
            }

            if (illness == IllnessCatalog.Flu && r.Health < FrailHealth)
            {
                r.IllnessId = IllnessCatalog.Pneumonia.Id;
                r.IllnessMinutesLeft = IllnessCatalog.Pneumonia.RecoveryMinutes;
                r.IllnessTreated = false;
                return HealthEvent.Worsened;
            }

            if (improving)
            {
                r.IllnessMinutesLeft -= resting ? 2 : 1;
                if (r.IllnessMinutesLeft <= 0)
                {
                    r.IllnessId = null;
                    r.IllnessMinutesLeft = 0;
                    r.IllnessTreated = false;
                    return HealthEvent.Recovered;
                }
            }

            return HealthEvent.None;
        }

        /// <summary>Today's chance of falling ill: poor health, exhaustion, poor hygiene, age and recent contact with someone ill all add to it.</summary>
        public static double IllnessRisk(Resident r, int day)
        {
            var risk = 0.008;
            risk += Math.Max(0, 70 - r.Health) * 0.004;
            if (r.Needs[NeedType.Energy] < 25)
            {
                risk += 0.05;
            }

            if (r.Needs[NeedType.Hygiene] < 25)
            {
                risk += 0.05;
            }

            if (r.ExposedDay > 0 && day - r.ExposedDay <= ExposureDays)
            {
                risk += 0.12;
            }

            if (r.LifeStage == LifeStage.Elderly)
            {
                risk += 0.01;
            }

            return risk;
        }

        /// <summary>The daily check: whether the resident falls ill today, and with what. Null if they stay well.</summary>
        public static Illness CatchesIllness(Resident r, int day)
        {
            if (r.IllnessId != null || Chance.Roll(r.Id, day, 1) >= IllnessRisk(r, day))
            {
                return null;
            }

            return Chance.Roll(r.Id, day, 2) < 0.65 ? IllnessCatalog.Cold : IllnessCatalog.Flu;
        }

        /// <summary>The chance of dying of old age on any one day.</summary>
        public static double OldAgeChancePerDay(int age) => age < OldAgeFrom ? 0 : (age - OldAgeFrom + 1) * OldAgeChancePerYear / DaysPerYear;

        public static bool DiesOfOldAge(Resident r, int day) => Chance.Roll(r.Id, day, 3) < OldAgeChancePerDay(r.AgeYears);
    }

    public enum HealthEvent
    {
        None,
        Recovered,

        /// <summary>Flu turned into pneumonia.</summary>
        Worsened,
        Died,
    }
}
