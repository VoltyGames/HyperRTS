using Unity.Entities;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>What a unit, building or tree is made of, for the effects of shots that hit it.</summary>
    public struct HitMaterial : IComponentData
    {
        public SurfaceMaterial Value;
    }
}
