using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>
    /// The clip a HyperRTS/LitTeam render entity plays, and the packing of every bone clip. Fps carries the loop flag
    /// in its sign: positive loops, negative plays once and holds the last row.
    /// </summary>
    [MaterialProperty("_VATClip")]
    public struct PlayingClip : IComponentData
    {
        /// <summary>x start row, y row count, z signed fps, w start time on the <see cref="ClipClockSystem"/> clock.</summary>
        public float4 Value;

        public static PlayingClip Started(in ClipRange clip, float now) => new() { Value = clip.Packed(now) };

        /// <summary>Plays <paramref name="clip"/> at <paramref name="rate"/> times its speed as if it began at <paramref name="startTime"/>, fading from the pose showing at <paramref name="now"/>.</summary>
        public void Switch(in ClipRange clip, float startTime, float now, float rate, ref ClipFade fade)
        {
            fade = ClipFade.From(BlendAt(now), now);
            Value = clip.Packed(startTime, rate);
        }

        /// <summary>The rows LitTeamVAT.hlsl and LitTeamBones.hlsl blend at <paramref name="time"/>; the shader does the same sums.</summary>
        public ClipBlend BlendAt(float time)
        {
            var rows = Value.y;
            var position = math.max(time - Value.w, 0f) * math.abs(Value.z);
            var loops = Value.z > 0f;
            var row = loops ? position % rows : math.min(position, rows - 1f);
            var first = math.floor(row);
            var next = loops ? (first + 1f) % rows : math.min(first + 1f, rows - 1f);
            return new ClipBlend { Row = (int)(Value.x + first), NextRow = (int)(Value.x + next), Blend = row - first };
        }
    }

    /// <summary>
    /// The pose a <see cref="PlayingClip"/> switch fades out of, held by <c>LowerFade</c> and <c>UpperFade</c>, which
    /// LitTeamBones.hlsl blends into the new clip over the material's _BoneFadeTime.
    /// </summary>
    public struct ClipFade
    {
        /// <summary>x row, y next row, z blend of the old pose, frozen at the switch; w the switch time on the clock.</summary>
        public float4 Value;

        /// <summary>No fade: a start time far enough back that the new clip is long fully shown.</summary>
        public static ClipFade None => new() { Value = new float4(0f, 0f, 0f, -100000f) };

        public static ClipFade From(in ClipBlend pose, float now) => new() { Value = new float4(pose.Row, pose.NextRow, pose.Blend, now) };
    }

    /// <summary>Two texture rows and how far to blend from the first to the second.</summary>
    public struct ClipBlend
    {
        public int Row;
        public int NextRow;
        public float Blend;
    }

    /// <summary>Object-space bounds of every row, which <see cref="ClipBoundsBakingSystem"/> gives the entity's RenderBounds.</summary>
    [BakingType]
    public struct ClipBounds : IComponentData
    {
        public AABB Value;
    }
}
