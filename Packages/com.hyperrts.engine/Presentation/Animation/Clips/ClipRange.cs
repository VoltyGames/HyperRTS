using Unity.Mathematics;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>One clip's rows in an animation texture, as game code keeps it to play.</summary>
    public struct ClipRange
    {
        public int StartRow;
        public int FrameCount;
        public float Fps;
        public bool Loop;

        /// <summary>Metres per second the clip's figure travels, from baking; 0 plays the clip at its own rate whatever the figure's speed.</summary>
        public float GroundSpeed;

        /// <summary>The material value starting the clip at <paramref name="startTime"/>, played <paramref name="rate"/> times its own speed.</summary>
        public readonly float4 Packed(float startTime, float rate = 1f) => new(StartRow, FrameCount, (Loop ? Fps : -Fps) * rate, startTime);

        public readonly bool SameAs(in ClipRange other) =>
            StartRow == other.StartRow && FrameCount == other.FrameCount && Fps == other.Fps && Loop == other.Loop;
    }
}
