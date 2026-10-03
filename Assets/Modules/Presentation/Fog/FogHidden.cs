using Unity.Entities;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Root hidden by fog of war (its meshes carry <c>DisableRendering</c>). HUD and overlays skip it too.</summary>
    public struct FogHidden : IComponentData { }
}
