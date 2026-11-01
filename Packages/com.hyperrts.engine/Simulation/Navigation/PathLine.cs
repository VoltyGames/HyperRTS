using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>A drawn path as its corners and the distance along it to each, read by distance.</summary>
    internal struct PathLine : IDisposable
    {
        public NativeList<float2> Corners;

        /// <summary>Path distance to each corner.</summary>
        public NativeList<float> Distances;

        public PathLine(Allocator allocator)
        {
            Corners = new NativeList<float2>(16, allocator);
            Distances = new NativeList<float>(16, allocator);
        }

        public void Dispose()
        {
            Corners.Dispose();
            Distances.Dispose();
        }

        public readonly float Total => Distances[Distances.Length - 1];

        public readonly float2 End => Corners[Corners.Length - 1];

        /// <summary>Rebuilds the line from <paramref name="start"/> through the points, skipping repeats.</summary>
        public void Trace(float2 start, NativeList<float3> points)
        {
            Corners.Clear();
            Distances.Clear();
            Corners.Add(start);
            Distances.Add(0f);
            foreach (var point in points)
            {
                var step = math.distance(Corners[Corners.Length - 1], point.xz);
                if (step > 1e-3f)
                {
                    Corners.Add(point.xz);
                    Distances.Add(Total + step);
                }
            }
        }

        public readonly float2 PointAt(float distance)
        {
            var leg = LegAt(distance);
            return Corners[leg] + HeadingAt(distance) * (distance - Distances[leg]);
        }

        public readonly float2 HeadingAt(float distance) => LegHeading(LegAt(distance));

        public readonly float2 LegHeading(int leg) => math.normalize(Corners[leg + 1] - Corners[leg]);

        public readonly int LegAt(float distance)
        {
            var leg = 0;
            while (leg < Corners.Length - 2 && Distances[leg + 1] <= distance)
            {
                leg++;
            }

            return leg;
        }

        /// <summary>The angle the path turns through at a corner between two legs.</summary>
        public readonly float TurnAt(int corner) =>
            math.acos(math.clamp(math.dot(LegHeading(corner - 1), LegHeading(corner)), -1f, 1f));
    }
}
