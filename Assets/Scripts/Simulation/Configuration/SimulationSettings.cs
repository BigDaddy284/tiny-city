using System;

namespace TinyTown.Simulation.Configuration
{
    /// <summary>Optional parts of the simulation the player can switch off.</summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        /// <summary>Whether residents can fall for each other, date and become couples.</summary>
        public bool romance = true;

        /// <summary>Whether the calendar's community events (get-togethers, quiz nights, the fête) take place.</summary>
        public bool communityEvents = true;
    }
}
