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

        public readonly float Middle => 0.5f * (Floor + Reach);
    }
}
