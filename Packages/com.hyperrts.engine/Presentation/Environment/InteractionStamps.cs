using System.Collections.Generic;
using HyperRTS.Simulation.Cues;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>One frame's stamps for the interaction maps.</summary>
    public sealed class InteractionStamps
    {
        public readonly List<InteractionStamp> Trample = new();
        public readonly List<InteractionStamp> Tracks = new();

        public readonly List<InteractionStamp> Scars = new();

        public readonly List<InteractionStamp> Wakes = new();

        /// <summary>Clear every map before drawing: a replay sought back past the scars.</summary>
        public bool Reset;

        /// <summary>Scar value: (depth / max, scorch, bowl radius / stamp radius, churn).</summary>
        public void AddCrater(float2 center, ImpactMark mark, float diameter)
        {
            var radius = diameter * 0.5f;
            var reach = mark.scorch > 0f ? radius * InteractionMath.ScorchReach : radius;
            var disc = new InteractionStamp { Center = center, Forward = new float2(0f, 1f), HalfSize = new float2(reach) };
            disc.Value = new float4(1f, 1f, 0f, 1f);
            Trample.Add(disc);
            var depth = math.saturate(mark.craterDepth / InteractionMath.MaxCraterDepth);
            disc.Value = new float4(depth, math.saturate(mark.scorch), radius / reach, 1f);
            Scars.Add(disc);
        }

        /// <summary>A footprint scorched and churned to <paramref name="share"/>: square, with no bowl.</summary>
        public void AddFootprintScar(float2 center, float2 forward, float2 size, float share) =>
            Scars.Add(new InteractionStamp
            {
                Center = center, Forward = forward, HalfSize = size * 0.5f, Value = new float4(0f, share, 0f, share),
            });

        public void AddWake(float2 position, float2 direction, float speed, float radius)
        {
            var halfLength = radius + speed * InteractionMath.WakeReachSeconds * 0.5f;
            Wakes.Add(new InteractionStamp
            {
                Center = position - direction * (halfLength - radius),
                Forward = direction,
                HalfSize = new float2(radius * 1.3f, halfLength),
                Value = new float4(math.saturate(speed / InteractionMath.FullWakeSpeed), 0f, 0f, 0f),
            });
        }

        public void Clear()
        {
            Trample.Clear();
            Tracks.Clear();
            Scars.Clear();
            Wakes.Clear();
            Reset = false;
        }
    }
}
