using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Fields;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Grid
{
    public partial struct NavGridSystem
    {
        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NavGrid Grid;
            public NativeArray<byte> Base;
            public bool Flood;
            public float MaxGradient;

            public void Execute(int index) => Base[index] = (byte)Grid.GroundSurface(index, Flood, MaxGradient);
        }

        /// <summary>Keeps the last stamp to compare with, then starts again from the ground.</summary>
        [BurstCompile]
        private struct ResetJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Base;
            public NativeArray<byte> Cells;
            public NativeArray<byte> Previous;

            public void Execute()
            {
                Previous.CopyFrom(Cells);
                Cells.CopyFrom(Base);
            }
        }

        [BurstCompile]
        private struct ChangeJob : IJob
        {
            public NavGrid Grid;

            public void Execute() => Grid.RecordChanges();
        }

        /// <summary>Single-threaded: overlapping areas write the same cells. Decks go last so they span water areas.</summary>
        [BurstCompile]
        private partial struct AreaJob : IJobEntity
        {
            public NavGrid Grid;
            public bool Decks;
            [ReadOnly] public BufferLookup<NavAreaPart> Parts;

            private void Execute(Entity entity, in NavArea area, in LocalTransform transform)
            {
                using var marker = RestampMarker.Auto();
                if ((area.Kind == NavAreaKind.Deck) != Decks)
                {
                    return;
                }

                if (!Parts.TryGetBuffer(entity, out var parts))
                {
                    Grid.StampArea(new NavBox(transform.Position, transform.Rotation, area.Size), area.Kind,
                        transform.Position.y);
                    return;
                }

                foreach (var part in parts)
                {
                    if (part.Open)
                    {
                        Grid.StampArea(part.Box(transform), area.Kind, transform.Position.y);
                    }
                }
            }
        }

        /// <summary>Single-threaded: overlapping footprints write the same cells.</summary>
        [BurstCompile]
        private partial struct ObstacleJob : IJobEntity
        {
            public NavGrid Grid;
            [ReadOnly] public BufferLookup<NavObstaclePart> Parts;

            private void Execute(Entity entity, in NavObstacle obstacle, in LocalTransform transform)
            {
                if (obstacle.Open)
                {
                    return;
                }

                using var marker = RestampMarker.Auto();
                if (!Parts.TryGetBuffer(entity, out var parts))
                {
                    Grid.StampArea(new NavBox(transform.Position, transform.Rotation, obstacle.Size), NavAreaKind.Blocked, 0f);
                    return;
                }

                foreach (var part in parts)
                {
                    if (part.Closed)
                    {
                        Grid.StampArea(part.Box(transform), NavAreaKind.Blocked, 0f);
                    }
                }
            }
        }

        /// <summary>Single-threaded: blockers may share cells. Disabled ones are skipped.</summary>
        [BurstCompile]
        private partial struct BlockerJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Blocker blocker, in LocalTransform transform)
            {
                using var marker = RestampMarker.Auto();
                blocker.Stamp(ref Grid, transform);
            }
        }

        /// <summary>Marks the field sectors round scenery that changed, then keeps this fill to compare with.</summary>
        [BurstCompile]
        private struct SceneryDiffJob : IJob
        {
            public NavField Field;
            [ReadOnly] public NativeArray<byte> Scenery;
            public NativeArray<byte> Previous;

            public void Execute() => Field.MarkChanges(Scenery, Previous);
        }

        /// <summary>Single-threaded: scenery sharing a cell keeps the sturdiest rating. Each piece fills its own cell.</summary>
        [BurstCompile]
        private partial struct SceneryJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Destructible destructible, in LocalTransform transform)
            {
                var cell = Grid.Grid.Cell(transform.Position);
                if (destructible.Crush <= 0 || !Grid.Grid.Contains(cell))
                {
                    return;
                }

                var index = Grid.Grid.Index(cell);
                var rating = math.min(destructible.Crush, NavAccess.AllScenery - 1);
                Grid.Scenery[index] = (byte)math.max((int)Grid.Scenery[index], rating);
            }
        }

        /// <summary>Single-threaded: an open obstacle with a crush rating fills every cell under its box.</summary>
        [BurstCompile]
        private partial struct OpenSceneryJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Destructible destructible, in NavObstacle obstacle, in LocalTransform transform)
            {
                if (obstacle.Open && destructible.Crush > 0)
                {
                    var rating = (byte)math.min(destructible.Crush, NavAccess.AllScenery - 1);
                    Grid.StampScenery(new NavBox(transform.Position, transform.Rotation, obstacle.Size), rating);
                }
            }
        }
    }
}
