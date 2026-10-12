namespace HyperRTS.Simulation.Resources
{
    /// <summary>A repeating timer kept in a component's <c>Elapsed</c> field.</summary>
    public static class IntervalTimer
    {
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
