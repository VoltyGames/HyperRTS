using HyperRTS.Presentation.Animation.Clips;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// The clip the lower body of a bone-animated renderer plays, packed as <see cref="PlayingClip"/> does. Every part of a
    /// figure carries the same one, so its body and weapon move together.
    /// </summary>
    [MaterialProperty("_LowerClip")]
    public struct LowerClip : IComponentData
    {
        public PlayingClip Clip;
    }

    /// <summary>The clip the upper body plays, so a soldier can fire while the legs walk.</summary>
    [MaterialProperty("_UpperClip")]
    public struct UpperClip : IComponentData
    {
        public PlayingClip Clip;
    }

    /// <summary>The pose a <see cref="LowerClip"/> switch fades out of, which LitTeam blends into the new clip.</summary>
    [MaterialProperty("_LowerFade")]
    public struct LowerFade : IComponentData
    {
        public ClipFade Fade;
    }

    /// <summary>The pose an <see cref="UpperClip"/> switch fades out of.</summary>
    [MaterialProperty("_UpperFade")]
    public struct UpperFade : IComponentData
    {
        public ClipFade Fade;
    }
}
