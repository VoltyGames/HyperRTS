using HyperRTS.Simulation.Cues.Effects;
using System.Collections.Generic;
using HyperRTS.Simulation.Units;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Interaction
{
    /// <summary>One frame's stamps for the interaction maps.</summary>
    public sealed class InteractionStamps
    {
        /// <summary>Scorch disc reach as a multiple of the bowl radius.</summary>
        public const float ScorchReach = 1.25f;

        /// <summary>Metres at full scale in a scar's depth channel; deeper craters clip. Matches HYPERRTS_MAX_CRATER_DEPTH in InteractionGlobals.hlsl.</summary>
        public const float MaxCraterDepth = 2f;

        public const float FullWakeSpeed = 8f;
        public const float WakeReachSeconds = 0.5f;

        // Half width in metres of the strip one soldier treads, and how far it bends plants and crops.
        private const float FootWidth = 0.3f;
        private const float FootFlatten = 0.4f;
        private const float FootCropDamage = 0.15f;

        public readonly List<InteractionStamp> Trample = new();
        public readonly List<InteractionStamp> Tracks = new();

        public readonly List<InteractionStamp> Scars = new();

        public readonly List<InteractionStamp> Remains = new();

        public readonly List<InteractionStamp> Wakes = new();

        /// <summary>Clear every map before drawing: a replay sought back past the scars.</summary>
        public bool Reset;

        /// <summary>Scar value: (depth / max, scorch, bowl radius / stamp radius, churn). Remains cover the scorch's reach.</summary>
        public void AddCrater(float2 center, ImpactMark mark, float diameter)
        {
            var radius = diameter * 0.5f;
            if (mark.LeavesRemains)
            {
                AddRemains(center, radius * ScorchReach, new float4(mark.rubble, mark.ash, mark.fuel, 0f));
            }

            var scarred = mark.craterDepth > 0f || mark.scorch > 0f;
            if (!scarred)
            {
                return;
            }

            var reach = mark.scorch > 0f ? radius * ScorchReach : radius;
            Trample.Add(Disc(center, reach, new float4(1f, 1f, 0f, 1f)));
            var depth = math.saturate(mark.craterDepth / MaxCraterDepth);
            Scars.Add(Disc(center, reach, new float4(depth, math.saturate(mark.scorch), radius / reach, 1f)));
        }

        /// <summary>A footprint scorched and churned to <paramref name="share"/>: square, with no bowl.</summary>
        public void AddFootprintScar(float2 center, float2 forward, float2 size, float share) =>
            Scars.Add(Stamp(center, forward, size * 0.5f, new float4(0f, share, 0f, share)));

        // A bowl as wide as the stamp but with no depth takes the brush's round, soft-edged path.
        /// <summary>A soft, ragged disc scorched to <paramref name="scorch"/>, with no depth and no churn: burnt grass.</summary>
        public void AddScorch(float2 center, float radius, float scorch) =>
            Scars.Add(Disc(center, radius, new float4(0f, math.saturate(scorch), 1f, 0f)));

        /// <summary>Rubble, ash, fuel and wet mud (0 to 1 each) left in a ragged disc for the rest of the match.</summary>
        public void AddRemains(float2 center, float radius, float4 amounts) =>
            Remains.Add(Disc(center, radius, math.saturate(amounts)));

        public void AddWake(float2 position, float2 direction, float speed, float radius)
        {
            var halfLength = radius + speed * WakeReachSeconds * 0.5f;
            Wakes.Add(Stamp(position - direction * (halfLength - radius), direction,
                new float2(radius * 1.3f, halfLength), new float4(math.saturate(speed / FullWakeSpeed), 0f, 0f, 0f)));
        }

        /// <summary>A vehicle's marks this frame, stretched back over <paramref name="advance"/> metres so fast vehicles leave no gaps.</summary>
        public void AddVehicle(in TrackMarks marks, float2 position, float2 forward, float advance)
        {
            AddTrample(marks, position, forward, advance);
            AddRuts(marks, position, forward, advance);
        }

        /// <summary>The grass flattened across the vehicle's whole width this frame.</summary>
        public void AddTrample(in TrackMarks marks, float2 position, float2 forward, float advance)
        {
            var center = position - forward * (advance * 0.5f);
            var span = (marks.Gauge + marks.Width) * 0.5f;
            var halfLength = (marks.Width + math.abs(advance)) * 0.5f;
            Trample.Add(Stamp(center, forward, new float2(span, math.max(halfLength, span)), new float4(1f, 1f, 1f, 0f)));
        }

        /// <summary>A rut under each side of one axle, or of the tracks, heading along <paramref name="forward"/>.</summary>
        public void AddRuts(in TrackMarks marks, float2 axle, float2 forward, float advance)
        {
            var center = axle - forward * (advance * 0.5f);
            var halfLength = (marks.Width + math.abs(advance)) * 0.5f;
            var rut = new float4(marks.RutDepth, 0.5f + 0.5f * marks.RutDepth, 0f, 0f);
            var halfSize = new float2(marks.Width * 0.5f, halfLength);
            var right = new float2(forward.y, -forward.x);
            var offset = right * (marks.Gauge * 0.5f);
            Tracks.Add(Stamp(center - offset, forward, halfSize, rut));
            Tracks.Add(Stamp(center + offset, forward, halfSize, rut));
        }

        /// <summary>A soldier's path this frame: a narrow strip that leans plants along the way, without flattening them.</summary>
        public void AddFootsteps(float2 feet, float2 forward, float stride) =>
            Trample.Add(Stamp(feet - forward * (stride * 0.5f), forward, new float2(FootWidth, FootWidth + stride * 0.5f),
                new float4(FootFlatten, FootCropDamage, 1f, 0f)));

        /// <summary>A building's footprint: plants under it stay flat and crops never come back.</summary>
        public void AddFootprint(float2 center, float2 size) =>
            Trample.Add(Stamp(center, new float2(0f, 1f), size * 0.5f, new float4(1f, 1f, 0f, 0f)));

        public void Clear()
        {
            Trample.Clear();
            Tracks.Clear();
            Scars.Clear();
            Remains.Clear();
            Wakes.Clear();
            Reset = false;
        }

        private static InteractionStamp Stamp(float2 center, float2 forward, float2 halfSize, float4 value) =>
            new() { Center = center, Forward = forward, HalfSize = halfSize, Value = value };

        private static InteractionStamp Disc(float2 center, float radius, float4 value) =>
            Stamp(center, new float2(0f, 1f), new float2(radius), value);
    }
}
