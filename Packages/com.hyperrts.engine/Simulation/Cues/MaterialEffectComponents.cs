using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>What a unit, building or tree is made of, for the effects of shots that hit it.</summary>
    public struct HitMaterial : IComponentData
    {
        public SurfaceMaterial Value;
    }

    /// <summary>What the entity's shots throw up where they land, by the material struck.</summary>
    public struct EntityImpactEffects : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;
    }
}
