using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Planning state of a unit's path.</summary>
    public enum PathStatus : byte
    {
        /// <summary>No path; the next enabled destination requests one.</summary>
        None = 0,

        /// <summary>Waiting for a pathfinding slot; the unit steers straight at its destination meanwhile.</summary>
        Requested = 1,
        Ready = 2,
    }

    /// <summary>Which destination and grid version a unit's <see cref="PathWaypoint"/>s were planned for.</summary>
    public struct PathState : IComponentData
    {
        public float3 Goal;
        public int GridVersion;
        public PathStatus Status;

        /// <summary>Frame of the pending request; older requests are served first.</summary>
        public uint RequestFrame;

        /// <summary>Seconds the unit has been walled off or out of sight of its next corner.</summary>
        public float Stuck;

        /// <summary>Set when <see cref="Stuck"/> runs out: the next search re-plans only the way to that corner.</summary>
        public bool Repair;

        /// <summary>Path length from the first <see cref="PathWaypoint"/> to the last, kept as corners change.</summary>
        public float LengthPastCorner;

        /// <summary>Nav cell where the corner after next was last out of sight; set only while <see cref="IsHidden"/>.</summary>
        public int2 HiddenFrom;

        /// <summary>The corner after next was out of sight from <see cref="HiddenFrom"/> on this grid version.</summary>
        public bool IsHidden;

        /// <summary>Path length from the first waypoint to the last.</summary>
        public static float Measure(DynamicBuffer<PathWaypoint> waypoints)
        {
            var length = 0f;
            for (var i = 1; i < waypoints.Length; i++)
            {
                length += math.distance(waypoints[i - 1].Position.xz, waypoints[i].Position.xz);
            }

            return length;
        }

        /// <summary>Whether the corner after next was already out of sight from this cell on this grid.</summary>
        public readonly bool IsHiddenFrom(int2 cell, int gridVersion) =>
            IsHidden && GridVersion == gridVersion && HiddenFrom.Equals(cell);

        public void Hide(int2 cell)
        {
            HiddenFrom = cell;
            IsHidden = true;
        }

        /// <summary>Drops the first waypoint, keeping <see cref="LengthPastCorner"/> in step.</summary>
        public void PassCorner(DynamicBuffer<PathWaypoint> waypoints)
        {
            LengthPastCorner = math.max(0f,
                LengthPastCorner - math.distance(waypoints[0].Position.xz, waypoints[1].Position.xz));
            IsHidden = false;
            waypoints.RemoveAt(0);
        }

        public void ClearWaypoints(DynamicBuffer<PathWaypoint> waypoints)
        {
            waypoints.Clear();
            LengthPastCorner = 0f;
            IsHidden = false;
        }

        /// <summary>Moves the last waypoint, keeping <see cref="LengthPastCorner"/> in step.</summary>
        public void MoveLast(DynamicBuffer<PathWaypoint> waypoints, float2 position)
        {
            var last = waypoints.Length - 1;
            if (last > 0)
            {
                var before = waypoints[last - 1].Position.xz;
                LengthPastCorner += math.distance(before, position) - math.distance(before, waypoints[last].Position.xz);
            }

            // The corner after next is the one moving, so what was hidden may now be in sight.
            IsHidden &= last != 1;
            ref var point = ref waypoints.ElementAt(last);
            point.Position.xz = position;
        }
    }
}
