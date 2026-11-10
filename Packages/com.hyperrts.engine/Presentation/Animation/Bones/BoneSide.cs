using HyperRTS.Presentation.Animation.Clips;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Bones
{
    /// <summary>
    /// The rows one half of the body blends at a time and how much of its clip shows over the pose it fades out of, as
    /// BoneSide in LitTeamBones.hlsl.
    /// </summary>
    public struct BoneSide
    {
        public float3 Frame;
        public float3 Old;
        public float Shown;

        /// <summary>The side playing <paramref name="clip"/> at <paramref name="time"/>, faded in over <paramref name="fadeTime"/>.</summary>
        public static BoneSide At(in PlayingClip clip, in ClipFade fade, float time, float fadeTime)
        {
            var blend = clip.BlendAt(time);
            return new BoneSide
            {
                Frame = new float3(blend.Row, blend.NextRow, blend.Blend),
                Old = fade.Value.xyz,
                Shown = math.saturate((time - fade.Value.w) / math.max(fadeTime, 0.0001f)),
            };
        }
    }
}
