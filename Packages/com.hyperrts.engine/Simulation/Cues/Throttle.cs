namespace HyperRTS.Simulation.Cues
{
    /// <summary>A "not before" time that keeps a cue from repeating too often.</summary>
    public struct Throttle
    {
        public double Next;

        public readonly bool IsOpen(double now) => now >= Next;

        /// <summary>Shuts the throttle for <paramref name="interval"/> seconds from <paramref name="now"/>.</summary>
        public void Close(double now, double interval) => Next = now + interval;

        /// <summary>True, closing the throttle, when it is open.</summary>
        public bool TryPass(double now, double interval)
        {
            if (!IsOpen(now))
            {
                return false;
            }

            Close(now, interval);
            return true;
        }
    }
}
