using System;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// One formation move's <see cref="FlowField"/> for one movement class, toward the centre of its members' slots.
    /// Members trace it from where they stand and leave it for their own slot once in sight of it.
    /// </summary>
    public struct GroupField
    {
        /// <summary><see cref="MoveDestination.Group"/>, then the class index.</summary>
        public int2 Key;

        public FlowField Field;

        /// <summary>The goal area's centre, the field's goal.</summary>
        public float3 Anchor;

        /// <summary>How far from the anchor the members' slots lie.</summary>
        public float Radius;

        /// <summary>The first member's <see cref="NavAgent.WallMargin"/>, which every trace pays with.</summary>
        public float WallMargin;

        /// <summary>The grid version the field is valid for.</summary>
        public int Version;

        public double LastUsed;

        /// <summary>Whether a member's query can trace this field: its key, a slot in the goal area, a start in reach.</summary>
        public readonly bool Serves(int2 key, in PathQuery query, float cellSize) =>
            Key.Equals(key) && math.distance(query.End.xz, Anchor.xz) <= Radius + cellSize && Field.Reaches(query.From);

        /// <summary>The class's view of the grid the field was built with.</summary>
        public readonly NavAccess Access(in NavGrid grid) => new(grid, grid.Classes.Classes[Key.y], WallMargin);

        /// <summary>
        /// The member's path: down the field to its first cell in the goal area that sees the slot, then to the slot,
        /// pulled tight. False when the field no longer leads down from the member's cell.
        /// </summary>
        public readonly bool Trace(in NavGrid grid, in PathQuery query, NativeList<float3> points)
        {
            var access = Access(grid);
            var cells = new NativeList<int2>(256, Allocator.Temp);
            var traced = Field.Trace(access, query.From, cells);
            if (traced)
            {
                var leave = cells.Length - 1;
                var area = Radius + access.Grid.CellSize;
                for (var i = cells.Length - 1; i >= 0; i--)
                {
                    var center = access.Grid.CellCenter(cells[i]);
                    if (math.distance(center.xz, Anchor.xz) > area)
                    {
                        break;
                    }

                    leave = access.HasLineOfSight(center, query.End) ? i : leave;
                }

                cells.Length = leave + 1;
                cells.Add(query.To);
                query.Begin(points);
                GridPathfinder.Smooth(access, query.Origin, query.End, cells, points);
            }

            cells.Dispose();
            return traced;
        }
    }

    /// <summary>
    /// The live <see cref="GroupField"/>s, keyed by (group, class). A field goes once a grid change reaches its
    /// corridor, within a second of no moving unit having its key, or after <see cref="IdleSeconds"/> unused; members
    /// asking again then get a new one.
    /// </summary>
    public struct GroupFields : IDisposable
    {
        /// <summary>Most cells one field covers; a larger group splits by start sector into several fields.</summary>
        public const int MaxCells = 60000;

        public const double IdleSeconds = 30.0;

        /// <summary>Fewest members a field serves: one alone is cheaper with its own search.</summary>
        public const int MinMembers = 2;

        /// <summary>Farthest a member's slot may lie from the group's centre to join its field, in metres.</summary>
        public const float MaxGoalArea = 48f;

        public NativeList<GroupField> Fields;

        public GroupFields(Allocator allocator) => Fields = new NativeList<GroupField>(16, allocator);

        public readonly bool IsCreated => Fields.IsCreated;

        public void Dispose()
        {
            foreach (var field in Fields)
            {
                field.Field.Dispose();
            }

            Fields.Dispose();
        }

        /// <summary>The field that serves a member's query, or -1.</summary>
        public readonly int Find(int2 key, in PathQuery query, float cellSize)
        {
            for (var i = 0; i < Fields.Length; i++)
            {
                if (Fields[i].Serves(key, query, cellSize))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Drops the fields no moving unit has the key of.</summary>
        public void DropUnused(in NativeParallelHashSet<int2> used)
        {
            for (var i = Fields.Length - 1; i >= 0; i--)
            {
                var field = Fields[i];
                if (!used.Contains(field.Key))
                {
                    field.Field.Dispose();
                    Fields.RemoveAtSwapBack(i);
                }
            }
        }

        /// <summary>Drops idle fields and ones a grid change reached; keeps the rest current.</summary>
        public void Prune(in NavGrid grid, double now)
        {
            for (var i = Fields.Length - 1; i >= 0; i--)
            {
                var field = Fields[i];
                var stale = now - field.LastUsed > IdleSeconds || !grid.Changes.Covers(field.Version, grid.Version) ||
                            IsTouched(field, grid);
                if (stale)
                {
                    field.Field.Dispose();
                    Fields.RemoveAtSwapBack(i);
                    continue;
                }

                field.Version = grid.Version;
                Fields[i] = field;
            }
        }

        // A change within the field's halo of the corridor can move its costs.
        private static bool IsTouched(in GroupField field, in NavGrid grid)
        {
            for (var version = field.Version + 1; version <= grid.Version; version++)
            {
                var area = grid.Changes.Areas[version % NavChanges.History];
                var changed = math.all(area.xy <= area.zw);
                if (changed && field.Field.Corridor.Touches(area.xy, area.zw, grid.Field.ReachCells))
                {
                    return true;
                }
            }

            return false;
        }

        public void MarkUsed(int index, double now)
        {
            var field = Fields[index];
            field.LastUsed = now;
            Fields[index] = field;
        }
    }
}
