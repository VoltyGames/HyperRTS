using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
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

            private void Execute(in NavArea area, in LocalTransform transform)
            {
                if ((area.Kind == NavAreaKind.Deck) == Decks)
                {
                    var box = new NavBox(transform.Position, transform.Rotation, area.Size);
                    Grid.StampArea(box, area.Kind, transform.Position.y);
                }
            }
        }

        /// <summary>Single-threaded: overlapping footprints write the same cells.</summary>
        [BurstCompile]
        private partial struct ObstacleJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in NavObstacle obstacle, in LocalTransform transform)
            {
                var box = new NavBox(transform.Position, transform.Rotation, obstacle.Size);
                Grid.StampArea(box, NavAreaKind.Blocked, 0f);
            }
        }

        /// <summary>Single-threaded: blockers may share cells. Disabled ones are skipped.</summary>
        [BurstCompile]
        private partial struct BlockerJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Blocker blocker, in LocalTransform transform) => blocker.Stamp(ref Grid, transform);
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

        [BurstCompile]
        private struct ClearSceneryJob : IJobParallelFor
        {
            public NativeArray<byte> Scenery;

            public void Execute(int index) => Scenery[index] = 0;
        }

        /// <summary>Single-threaded: scenery sharing a cell keeps the sturdiest rating. Each piece fills its own cell.</summary>
        [BurstCompile]
        private partial struct SceneryJob : IJobEntity
        {
            public NavGrid Grid;

            private void Execute(in Destructible destructible, in LocalTransform transform)
            {
                var cell = Grid.WorldToCell(transform.Position);
                if (destructible.Crush <= 0 || !Grid.InBounds(cell))
                {
                    return;
                }

                var index = Grid.Index(cell);
                var rating = math.min(destructible.Crush, NavAccess.AllScenery - 1);
                Grid.Scenery[index] = (byte)math.max((int)Grid.Scenery[index], rating);
            }
        }
    }
}
