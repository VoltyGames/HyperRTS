namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>The shares of a hit's damage and suppression a squad member takes after its cover.</summary>
    public struct Exposure
    {
        public float Damage;
        public float Suppression;

        /// <summary>No cover: all of it.</summary>
        public static Exposure Open => new() { Damage = 1f, Suppression = 1f };
    }
}
