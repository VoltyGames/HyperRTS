using Unity.Entities;

namespace HyperRTS.Core.Attack
{
    /// <summary>
    /// Component for melee attack damage.
    /// </summary>
    public struct MeleeAttackDamage : IComponentData
    {
        public int Value;
    }
}
