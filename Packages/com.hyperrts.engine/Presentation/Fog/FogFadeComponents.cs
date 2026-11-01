using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>HyperRTS/LitTeam's share of a mesh still drawn as its root fades out of or into the fog; 1 fully shown.</summary>
    [MaterialProperty("_Reveal")]
    public struct RevealFade : IComponentData
    {
        public float Value;
    }

    /// <summary>A root the local player has in sight or is watching fade out, so leaving sight fades it.</summary>
    public struct FogShown : IComponentData
    {
    }

    /// <summary>A root fading toward hidden while <c>FogHidden</c>, else toward shown.</summary>
    public struct FogFading : IComponentData
    {
        public float Reveal;
    }
}
