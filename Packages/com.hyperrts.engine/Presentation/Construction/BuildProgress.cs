using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Construction
{
    /// <summary>HyperRTS/LitTeam's construction progress on a render entity, 0..1; meshes without it show whole.</summary>
    [MaterialProperty("_BuildProgress")]
    public struct BuildProgress : IComponentData
    {
        public float Value;
    }

    /// <summary>Marks a building root whose meshes carry <see cref="BuildProgress"/>; client-only bookkeeping.</summary>
    public struct BuildRevealing : IComponentData
    {
    }
}
