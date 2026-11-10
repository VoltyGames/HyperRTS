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

    /// <summary>
    /// The live pose slot a bone-animated renderer draws, which LitTeam reads in place of its clips; below 0 it plays
    /// its clips. The material default is <see cref="None"/> too, since slot 0 is a real slot.
    /// </summary>
    [MaterialProperty("_PoseSlot")]
    public struct PoseSlot : IComponentData
    {
        public const float None = -1f;

        public float Value;
    }
}
