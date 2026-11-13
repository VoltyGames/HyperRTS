using System;
using HyperRTS.Simulation.Navigation.Agents;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Drive
{
    /// <summary>A drive plan's waypoints at one height, and how far along the drawn path they reach.</summary>
    internal struct WaypointWriter : IDisposable
    {
        public NativeList<PathWaypoint> Waypoints;
        public float Height;

        /// <summary>Path distance the waypoints already cover.</summary>
        public float Written;

        public WaypointWriter(Allocator allocator)
        {
            Waypoints = new NativeList<PathWaypoint>(64, allocator);
            Height = 0f;
            Written = 0f;
        }

        public void Dispose() => Waypoints.Dispose();

        /// <summary>Starts a plan at <paramref name="height"/> with no waypoints.</summary>
        public void Begin(float height)
        {
            Waypoints.Clear();
            Height = height;
            Written = 0f;
        }

        public void AddPoint(float2 point) =>
            Waypoints.Add(new PathWaypoint { Position = new float3(point.x, Height, point.y) });

        /// <summary>The drawn corners after what is written and before <paramref name="before"/>.</summary>
        public void AddDrawn(in PathLine line, float before)
        {
            for (var corner = 1; corner < line.Corners.Length; corner++)
            {
                var at = line.Distances[corner];
                if (at > Written && at < before)
                {
                    AddPoint(line.Corners[corner]);
                }
            }

            Written = math.max(Written, math.min(before, line.Total));
        }

        /// <summary>
        /// A stretch that doesn't follow on from the last starts at its pose on the path, a drawn point: the way up to
        /// it is an ordinary leg, however long.
        /// </summary>
        public void BeginStretch(in PathLine line, float from)
        {
            var joins = from <= Written;
            AddDrawn(line, from);
            if (!joins)
            {
                AddPoint(line.PointAt(from));
            }
        }
    }
}
