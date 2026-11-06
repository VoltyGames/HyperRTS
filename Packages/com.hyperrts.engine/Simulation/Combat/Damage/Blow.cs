using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>One strike of a <see cref="DamageEvent"/> on one target: the direct hit, or the splash share that reaches it.</summary>
    public struct Blow
    {
        /// <summary>Damage before armor; negative heals.</summary>
        public float Amount;

        /// <summary>Where it comes from, for directional armor and cover.</summary>
        public float3 From;

        /// <summary>Rolls which squad member a direct hit lands on.</summary>
        public uint Seed;

        /// <summary>The squad member it lands on; -1 rolls one by <see cref="Seed"/>.</summary>
        public int Member;

        /// <summary>Suppression it adds to a squad, before cover.</summary>
        public float Suppression;
    }
}
