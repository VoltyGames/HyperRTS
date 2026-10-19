using Unity.Entities;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>The copy drawing a <c>LastSeen</c> memory; a cleanup component, so the copy goes with the memory.</summary>
    public struct FogMemoryVisual : ICleanupComponentData
    {
        public Entity Root;
    }
}
