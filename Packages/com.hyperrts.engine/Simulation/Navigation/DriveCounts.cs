namespace HyperRTS.Simulation.Navigation
{
    /// <summary>What the drive searches of one plan, or of every plan so far, cost.</summary>
    public struct DriveCounts
    {
        /// <summary>Drive searches run for stretches a chassis couldn't drive as drawn.</summary>
        public int Searches;

        /// <summary>Poses the drive searches expanded.</summary>
        public long Expansions;

        /// <summary>Searches that stopped at the cap or ran out of ways, leaving their stretch as drawn.</summary>
        public int Failures;

        public void Add(in DriveCounts other)
        {
            Searches += other.Searches;
            Expansions += other.Expansions;
            Failures += other.Failures;
        }
    }
}
