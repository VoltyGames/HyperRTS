namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>How near and how far a unit's weapons that join one attack can hit: the shortest minimum range and the longest reach.</summary>
    public struct RangeBand
    {
        public float Floor;
        public float Reach;

        public RangeBand(float floor, float reach)
        {
            Floor = floor;
            Reach = reach;
        }

        /// <summary>Whether a target <paramref name="gap"/> away, edge to edge, is neither too near nor too far.</summary>
        public readonly bool Contains(float gap) => gap >= Floor && gap <= Reach;

        public readonly float Middle => 0.5f * (Floor + Reach);
    }
}
