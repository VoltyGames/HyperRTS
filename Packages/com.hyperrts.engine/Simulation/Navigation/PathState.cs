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

        /// <summary>Set when the unit stops getting on: the next search re-plans only the way to its next corner.</summary>
        public bool Repair;

        /// <summary>The path ends short of the goal, so any grid change re-plans it in case the way opened.</summary>
        public bool IsShort;

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

        /// <summary>
        /// Passes corners within <paramref name="touch"/>, and nearer ones once the next is in sight: a crowd can't all
        /// touch the same corner and a vehicle cuts it.
        /// </summary>
        public void SkipCorners(in NavGrid grid, in NavAgent agent, float3 position, float touch, float reach,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            while (waypoints.Length > 1 && CanSkipCorner(grid, agent, position, touch, reach, waypoints))
            {
                PassCorner(waypoints);
            }
        }

        private bool CanSkipCorner(in NavGrid grid, in NavAgent agent, float3 position, float touch, float reach,
            DynamicBuffer<PathWaypoint> waypoints)
        {
            var distance = math.distance(position.xz, waypoints[0].Position.xz);
            if (distance <= touch)
            {
                return true;
            }

            if (distance > math.max(CornerSkipRange(agent.Radius), reach))
            {
                return false;
            }

            // Sight only changes as the unit crosses cells, so a failed look isn't repeated within one.
            var cell = grid.WorldToCell(position);
            if (IsHiddenFrom(cell, grid.Version))
            {
                return false;
            }

            if (agent.Access(grid).HasLineOfSight(position, waypoints[1].Position, true))
            {
                return true;
            }

            Hide(cell);
            return false;
        }

        // Only corners this near are skipped, so a unit keeps to its smoothed path rather than cutting across.
        private static float CornerSkipRange(float radius) => radius * 4f + 1f;

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
