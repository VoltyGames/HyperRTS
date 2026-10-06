using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>Unmodified values, captured from the live components the first time modifiers change.</summary>
    public struct BaseStats : IComponentData
    {
        public bool Captured;
        public float MaxHealth;
        public float Damage;
        public float Range;
        public float Cooldown;
        public float MoveSpeed;
        public float VisionRange;
        public float BuildRate;
        public float ProductionSpeed;
    }
}
