using NUnit.Framework;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    public class GameTimeTests
    {
        [Test]
        public void DayOne_StartsOnMonday_AtMidnight()
        {
            var t = new GameTime(0);

            Assert.That(t.Day, Is.EqualTo(1));
            Assert.That(t.Weekday, Is.EqualTo(Weekday.Monday));
            Assert.That(t.ClockText, Is.EqualTo("00:00"));
        }

        [Test]
        public void FromDayAndTime_RoundTrips()
        {
            var t = GameTime.FromDayAndTime(9, 17, 45);

            Assert.That(t.Day, Is.EqualTo(9));
            Assert.That(t.Hour, Is.EqualTo(17));
            Assert.That(t.Minute, Is.EqualTo(45));
            Assert.That(t.Weekday, Is.EqualTo(Weekday.Tuesday));
        }

        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(5, false)]
        [TestCase(8, false)]
        public void Weekend_IsSaturdayAndSunday(int day, bool weekend)
        {
            Assert.That(GameTime.FromDayAndTime(day, 12, 0).IsWeekend, Is.EqualTo(weekend));
        }

        [TestCase(23 * 60, true)]
        [TestCase(2 * 60, true)]
        [TestCase(6 * 60 + 29, true)]
        [TestCase(6 * 60 + 30, false)]
        [TestCase(12 * 60, false)]
        public void IsWithin_HandlesWindowsThatCrossMidnight(int minute, bool expected)
        {
            Assert.That(GameTime.IsWithin(minute, 22 * 60 + 30, 6 * 60 + 30), Is.EqualTo(expected));
        }

        [Test]
        public void AddMinutes_CrossesDays()
        {
            var t = GameTime.FromDayAndTime(1, 23, 59).AddMinutes(2);

            Assert.That(t.Day, Is.EqualTo(2));
            Assert.That(t.ClockText, Is.EqualTo("00:01"));
        }

        [Test]
        public void NegativeTime_IsRejected()
        {
            Assert.That(() => new GameTime(-1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
