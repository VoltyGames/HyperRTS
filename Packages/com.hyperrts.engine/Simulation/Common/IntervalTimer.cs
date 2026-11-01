namespace HyperRTS.Simulation.Common
{
    /// <summary>A repeating timer kept in a component's <c>Elapsed</c> field.</summary>
    public static class IntervalTimer
    {
        /// <summary>
        /// Intervals come due for an owned, working entity. A neutral one starts over, so a new owner waits a full
        /// interval; an offline one keeps its progress.
        /// </summary>
        public static int Due(ref float elapsed, float interval, float deltaTime, byte faction, bool offline)
        {
            if (faction == Faction.Neutral)
            {
                elapsed = 0f;
                return 0;
            }

            return offline ? 0 : Advance(ref elapsed, interval, deltaTime);
        }

        /// <summary>Advances <paramref name="elapsed"/> and returns how many whole intervals came due.</summary>
        public static int Advance(ref float elapsed, float interval, float deltaTime)
        {
            elapsed += deltaTime;
            var due = 0;
            while (elapsed >= interval)
            {
                elapsed -= interval;
                due++;
            }

            return due;
        }
    }
}
