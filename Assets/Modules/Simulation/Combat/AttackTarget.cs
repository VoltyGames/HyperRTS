using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>The entity being attacked. While enabled, combat owns the unit's movement (chase, stop to fire).</summary>
    public struct AttackTarget : IComponentData, IEnableableComponent
    {
        public Entity Value;
    }
}
