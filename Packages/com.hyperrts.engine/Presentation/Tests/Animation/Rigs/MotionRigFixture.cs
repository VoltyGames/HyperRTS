using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Linq;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Rigs
{
    /// <summary>A motion rig system in a bare world, with helpers to build a unit's rig, pose it and step frames.</summary>
    public abstract class MotionRigFixture
    {
        protected const float Step = 0.1f;

        protected World _world;
        protected EntityManager _em;
        protected SystemHandle _system;
        protected Entity _unit;
        protected Entity _grid;
        protected float3 _position;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Motion Rig Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<MotionRigSystem>();
            var cells = new CellGrid(float2.zero, 1f, new int2(32, 32));
            _grid = _em.CreateEntity();
            _em.AddComponentData(_grid, NavGrid.Create(cells, default, 0f));
            _position = new float3(10f, 0f, 10f);
        }

        [TearDown]
        public void TearDown()
        {
            var grid = _em.GetComponentData<NavGrid>(_grid);
            if (grid.Terrain.IsCreated)
            {
                grid.Terrain.Blob.Dispose();
            }

            grid.Dispose();
            _world.Dispose();
        }

        /// <summary>Lays a heightfield of 2 m samples under the nav grid.</summary>
        protected void SetTerrain(NativeArray<float> heights, int2 size)
        {
            var grid = _em.GetComponentData<NavGrid>(_grid);
            grid.Terrain = TerrainHeight.Create(heights, size, float2.zero, 2f, Allocator.Persistent);
            _em.SetComponentData(_grid, grid);
        }

        /// <summary>Lays a deck at <paramref name="height"/> over the cells from <paramref name="low"/> to <paramref name="high"/>.</summary>
        protected void SetDeck(int2 low, int2 high, float height)
        {
            var grid = _em.GetComponentData<NavGrid>(_grid);
            foreach (var cell in grid.Grid.Cells(low, high))
            {
                var index = grid.Grid.Index(cell);
                grid.Cells[index] = (byte)(NavSurface.Land | NavSurface.Deck);
                grid.DeckHeights[index] = height;
            }
        }

        protected Entity Rig(MotionRigPart part, params MotionRigPart[] more)
        {
            var parts = new[] { part }.Concat(more).ToArray();
            for (var i = 0; i < parts.Length; i++)
            {
                parts[i].Part = _em.CreateEntity();
                _em.AddComponentData(parts[i].Part, LocalTransform.FromPositionRotation(parts[i].RestPosition, quaternion.identity));
                parts[i].RestRotation = quaternion.identity;
            }

            _unit = _em.CreateEntity();
            _em.AddComponentData(_unit, LocalTransform.FromPosition(_position));
            _em.AddBuffer<WeaponPose>(_unit).Add(default);
            var writer = new EntityWriter(_em, _unit);
            MotionRigAuthoring.AddRig(writer, new MotionRig { TiltFrame = quaternion.identity, TiltRest = quaternion.identity },
                parts);
            MotionRigLink.AddAll(_em, _unit);
            return parts[0].Part;
        }

        protected void SetPose(WeaponPose pose)
        {
            var poses = _em.GetBuffer<WeaponPose>(_unit);
            poses[0] = pose;
        }

        protected void MoveTo(float3 position)
        {
            _em.SetComponentData(_unit, LocalTransform.FromPosition(position));
            Update(1);
        }

        protected MotionRigPart Part(int index) => _em.GetBuffer<MotionRigPart>(_unit)[index];

        protected LocalTransform Pose(Entity part) => _em.GetComponentData<LocalTransform>(part);

        protected void Update(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                _world.SetTime(new TimeData(_world.Time.ElapsedTime + Step, Step));
                _system.Update(_world.Unmanaged);
                _em.CompleteAllTrackedJobs();
            }
        }
    }
}
