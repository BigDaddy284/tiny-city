namespace TinyCity.Simulation.Activities
{
    /// <summary>
    /// One later step of an AI plan for free time: an action and target that were among the resident's
    /// <see cref="FreeTimeOption"/>s when the plan was made. It is only a wish; the game checks it again when it is due.
    /// </summary>
    public sealed class PlanStep
    {
        public PlanStep(string actionId, string targetId)
        {
            ActionId = actionId;
            TargetId = targetId;
        }

        public string ActionId { get; }

        /// <summary>The planned target, or null to take the nearest place for the action.</summary>
        public string TargetId { get; }
    }
}
