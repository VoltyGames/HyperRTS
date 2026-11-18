using HyperRTS.Simulation.Cues.Effects;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Stamps
{
    /// <summary>One frame's stamps for the trample maps.</summary>
    public sealed class TrampleStamps
    {
        /// <summary>Metres across the tracks map's alpha, from 0 to 1. Matches HYPERRTS_GROUND_RANGE in TrampleGlobals.hlsl.</summary>
        public const float GroundRange = 10f;

        /// <summary>Alpha of untouched ground: above it the ground sank, below it a rim lifted it. Matches HYPERRTS_GROUND_LEVEL_BIAS.</summary>
        public const float GroundLevelBias = 40f / 255f;

        /// <summary>Deepest bowl or trench and highest rim the alpha holds; more clips.</summary>
        public const float MaxDepth = 8f;

        public const float MaxRim = 1.5f;

        /// <summary>A rim fades out by this multiple of the bowl radius, so a stamp holding one reaches that far.</summary>
        public const float RimReach = 1.3f;

        public const float FullWakeSpeed = 8f;
        public const float WakeReachSeconds = 0.5f;

        // Half width in metres of the strip one soldier treads, and how far it bends plants and crops.
        private const float FootWidth = 0.3f;
        private const float FootFlatten = 0.4f;
        private const float FootCropDamage = 0.15f;

        public readonly List<TrampleStamp> Trample = new();
        public readonly List<TrampleStamp> Tracks = new();

        public readonly List<TrampleStamp> Scars = new();

        /// <summary>Ground levelled under buildings, drawn once and then dropped.</summary>
        public readonly List<TrampleStamp> Fills = new();

        public readonly List<TrampleStamp> Stains = new();

        public readonly List<TrampleStamp> Wakes = new();

        /// <summary>Clear every map before drawing: a replay sought back past the scars.</summary>
        public bool Reset;

        /// <summary>Scar value: (depth / range, scorch, bowl radius / stamp radius, churn), with the rim in <see cref="TrampleStamp.Rim"/>. The stain covers the burn reach.</summary>
        public void AddScar(float2 center, in GroundScar scar, float radius)
        {
            if ((scar.scorch > 0f || scar.LeavesStain) && scar.burnReach < RimReach)
            {
                throw new ArgumentException($"HyperRTS: a ground scar with scorch or a stain needs a burn reach of at least {RimReach}, got {scar.burnReach}.");
            }

            if (scar.LeavesStain)
            {
                AddStain(center, radius * scar.burnReach, scar.Stain);
            }

            var rim = math.min(scar.rimHeight, MaxRim);
            if (scar.craterDepth <= 0f && scar.scorch <= 0f && rim <= 0f)
            {
                return;
            }

            var reach = radius * ScarReach(scar, rim);
            Trample.Add(Disc(center, reach, new float4(1f, 1f, 0f, 1f)));
            var depth = math.min(scar.craterDepth, MaxDepth) / GroundRange;
            var stamp = Disc(center, reach, new float4(depth, math.saturate(scar.scorch), radius / reach, 1f));
            stamp.Rim = rim / GroundRange;
            Scars.Add(stamp);
        }

        /// <summary>A ditch <paramref name="depth"/> metres deep along <paramref name="forward"/>, sloping up at its sides; a negative bowl value marks it.</summary>
        public void AddTrench(float2 center, float2 forward, float2 halfSize, float depth) =>
            Scars.Add(Stamp(center, forward, halfSize, new float4(math.min(depth, MaxDepth) / GroundRange, 0f, -1f, 1f)));

        /// <summary>Ground level again under a building: no bowl or rim stays inside <paramref name="area"/>, easing out over <paramref name="skirt"/> metres.</summary>
        public void AddFill(in TurnedRect area, float skirt) =>
            Fills.Add(Stamp(area.Center, area.Forward, area.Half + skirt, new float4(area.Half, skirt, 0f)));

        /// <summary>A footprint scorched and churned to <paramref name="share"/>: square, with no bowl.</summary>
        public void AddFootprintScar(float2 center, float2 forward, float2 size, float share) =>
            Scars.Add(Stamp(center, forward, size * 0.5f, new float4(0f, share, 0f, share)));

        // A bowl as wide as the stamp but with no depth takes the brush's round, soft-edged path.
        /// <summary>A soft, ragged disc scorched to <paramref name="scorch"/>, with no depth and no churn: burnt grass.</summary>
        public void AddScorch(float2 center, float radius, float scorch) =>
            Scars.Add(Disc(center, radius, new float4(0f, math.saturate(scorch), 1f, 0f)));

        /// <summary>Rubble, ash, fuel and wet mud (0 to 1 each) left in a ragged disc for the rest of the match.</summary>
        public void AddStain(float2 center, float radius, float4 amounts) =>
            Stains.Add(Disc(center, radius, math.saturate(amounts)));

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
            Stains.Clear();
            Wakes.Clear();
            Reset = false;
        }

        // Scorch and the stain set how far a scar reaches; a bare bowl with a lip needs room for the lip.
        private static float ScarReach(in GroundScar scar, float rim)
        {
            if (scar.scorch > 0f)
            {
                return scar.burnReach;
            }

            return rim > 0f ? RimReach : 1f;
        }

        private static TrampleStamp Stamp(float2 center, float2 forward, float2 halfSize, float4 value) =>
            new() { Center = center, Forward = forward, HalfSize = halfSize, Value = value };

        private static TrampleStamp Disc(float2 center, float radius, float4 value) =>
            Stamp(center, new float2(0f, 1f), new float2(radius), value);
    }
}
