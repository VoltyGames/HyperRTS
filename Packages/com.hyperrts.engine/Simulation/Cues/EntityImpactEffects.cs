using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What the entity's shots throw up where they land, by the material struck.</summary>
    public struct EntityImpactEffects : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;
    }
}
