using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// The clip HyperRTS/LitTeam plays on a render entity with _VAT_RIGID on, and the packing of every bone clip. Fps
    /// carries the loop flag in its sign: positive loops, negative plays once and holds the last frame.
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
        public bool Play(in VATClipRange clip, float now, ref VATFade fade)
        {
            if (Plays(clip))
            {
                return false;
            }

            Switch(clip, now, now, ref fade);
            return true;
        }

        /// <summary>Plays <paramref name="clip"/> as if it began at <paramref name="startTime"/>, fading from the pose showing at <paramref name="now"/>.</summary>
        public void Switch(in VATClipRange clip, float startTime, float now, ref VATFade fade)
        {
            fade = VATFade.From(FrameAt(now), now);
            Value = clip.Packed(startTime);
        }

        /// <summary>The frames LitTeamVAT.hlsl and LitTeamBones.hlsl blend at <paramref name="time"/>; the shader does the same sums.</summary>
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

    /// <summary>
    /// The pose a <see cref="VATClip"/> switch fades out of, held by <see cref="LowerFade"/> and <see cref="UpperFade"/>,
    /// which LitTeamBones.hlsl blends into the new clip over the material's _BoneFadeTime.
    /// </summary>
    public struct VATFade
    {
        /// <summary>Seconds of the material's default _BoneFadeTime.</summary>
        public const float Seconds = 0.2f;

        /// <summary>x frame, y next frame, z blend of the old pose, frozen at the switch; w the switch time on the clock.</summary>
        public float4 Value;

        /// <summary>No fade: a start time far enough back that the new clip is long fully shown.</summary>
        public static VATFade None => new() { Value = new float4(0f, 0f, 0f, -100000f) };

        public static VATFade From(in VATFrame pose, float now) => new() { Value = new float4(pose.Frame, pose.NextFrame, pose.Blend, now) };

        /// <summary>0 while the old pose shows, 1 once the new clip has taken over.</summary>
        public readonly float Weight(float now, float seconds) => math.saturate((now - Value.w) / seconds);
    }

    /// <summary>One clip's frames in an animation texture, as game code keeps it to play. Frames count from StartRow.</summary>
    public struct VATClipRange
    {
        public int StartRow;
        public int FrameCount;
        public float Fps;
        public bool Loop;

        public float4 Packed(float startTime) => new(StartRow, FrameCount, Loop ? Fps : -Fps, startTime);

        public readonly bool SameAs(in VATClipRange other) =>
            StartRow == other.StartRow && FrameCount == other.FrameCount && Fps == other.Fps && Loop == other.Loop;
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
