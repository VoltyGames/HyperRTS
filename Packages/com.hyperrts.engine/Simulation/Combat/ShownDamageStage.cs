using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>The <see cref="DamageStage"/> on screen; presentation keeps it, -1 until the first frame shows one.</summary>
    public struct ShownDamageStage : IComponentData
    {
        public int Index;
    }
}
