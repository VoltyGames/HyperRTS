using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// The vertex animation clip HyperRTS/LitTeam plays on a render entity with _VAT on. Fps carries the loop flag in
    /// its sign: positive loops, negative plays once and holds the last frame.
    /// </summary>
    [MaterialProperty("_VATClip")]
    public struct VATClip : IComponentData
    {
        /// <summary>x start frame, y frame count, z signed fps, w start time on the <see cref="VATClockSystem"/> clock.</summary>
        public float4 Value;

        public static VATClip Started(in VATClipRange clip, float now) => new() { Value = clip.Packed(now) };

        /// <summary>True when <paramref name="clip"/> is the one playing, whenever it started.</summary>
        public bool Plays(in VATClipRange clip) => Value.xyz.Equals(clip.Packed(0f).xyz);

        /// <summary>Starts <paramref name="clip"/> at <paramref name="now"/> unless it already plays; true when it wrote.</summary>
        public bool Play(in VATClipRange clip, float now)
        {
            if (Plays(clip))
            {
                return false;
            }

            Value = clip.Packed(now);
            return true;
        }

        /// <summary>The frames LitTeamVAT.hlsl blends at <paramref name="time"/>; the shader does the same sums.</summary>
        public VATFrame FrameAt(float time)
        {
            var frames = Value.y;
            var position = math.max(time - Value.w, 0f) * math.abs(Value.z);
            var loops = Value.z > 0f;
            var frame = loops ? position % frames : math.min(position, frames - 1f);
            var first = math.floor(frame);
            var next = loops ? (first + 1f) % frames : math.min(first + 1f, frames - 1f);
            return new VATFrame { Frame = (int)(Value.x + first), NextFrame = (int)(Value.x + next), Blend = frame - first };
        }
    }

    /// <summary>One clip's frames in a vertex animation texture, as game code keeps it to play. Frames count from StartRow.</summary>
    public struct VATClipRange
    {
        public int StartRow;
        public int FrameCount;
        public float Fps;
        public bool Loop;

        public float4 Packed(float startTime) => new(StartRow, FrameCount, Loop ? Fps : -Fps, startTime);
    }

    /// <summary>Two frames, each rowsPerFrame texture rows, and how far to blend from the first to the second.</summary>
    public struct VATFrame
    {
        public int Frame;
        public int NextFrame;
        public float Blend;
    }

    /// <summary>Object-space bounds of every frame, which VATBoundsBakingSystem gives the entity's RenderBounds.</summary>
    [BakingType]
    public struct VATBounds : IComponentData
    {
        public AABB Value;
    }
}
