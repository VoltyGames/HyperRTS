using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using System;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.MatchStart;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>A test world whose players, cue queue and prefab registry come from the systems and setups a match uses.</summary>
    public sealed class PresentationWorldKit : IDisposable
    {
        private SystemHandle _cueClear;
        private SystemHandle _registry;

        public PresentationWorldKit(string name) => World = new World(name);

        public World World { get; }

        public EntityManager EntityManager => World.EntityManager;

        /// <summary>The entity holding the nav grid <see cref="AddGround"/> lays.</summary>
        public Entity Ground { get; private set; }

        public Entity AddPlayer(byte owner, float4 color = default, bool local = false, byte side = 0)
        {
            var player = EntityManager.CreateEntity();
            var writer = new EntityWriter(EntityManager, player);
            PlayerSetup.Add(writer, owner, $"P{owner}", color, populationCap: 0);
            EntityManager.SetComponentData(player, new PlayerSide { Value = side });
            if (local)
            {
                EntityManager.AddComponent<LocalPlayer>(player);
            }

            return player;
        }

        public void AddCues() => _cueClear = World.CreateSystem<CueClearSystem>();

        public void PushCue(in CueEvent cue)
        {
            using var query = EntityManager.CreateEntityQuery(typeof(CueQueue));
            EntityManager.GetBuffer<CueEvent>(query.GetSingletonEntity()).Add(cue);
        }

        public void ClearCues() => _cueClear.Update(World.Unmanaged);

        /// <summary>Creates the registry over the prefabs made so far.</summary>
        public PrefabRegistry AddRegistry()
        {
            _registry = World.CreateSystem<PrefabRegistrySystem>();
            return RefreshRegistry();
        }

        /// <summary>Picks up prefabs made since, as the registry system does each frame.</summary>
        public PrefabRegistry RefreshRegistry()
        {
            _registry.Update(World.Unmanaged);
            using var query = EntityManager.CreateEntityQuery(typeof(PrefabRegistry));
            return query.GetSingleton<PrefabRegistry>();
        }

        /// <summary>Lays a flat nav grid of 1 m cells, 64 m a side from the origin, for what stands on the ground.</summary>
        public void AddGround()
        {
            Ground = EntityManager.CreateEntity();
            EntityManager.AddComponentData(Ground, NavGrid.Create(new CellGrid(float2.zero, 1f, new int2(64, 64)), default, 0f));
        }

        /// <summary>Lays a heightfield of 2 m samples from the origin under the nav grid.</summary>
        public void SetTerrain(NativeArray<float> heights, int2 size)
        {
            var grid = EntityManager.GetComponentData<NavGrid>(Ground);
            grid.Terrain = TerrainHeight.Create(heights, size, float2.zero, 2f, Allocator.Persistent);
            EntityManager.SetComponentData(Ground, grid);
        }

        /// <summary>Lays ground rising half a metre a metre along x, on 2 m samples.</summary>
        public void RaiseGroundAlongX()
        {
            var size = new int2(33, 33);
            var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = i % size.x;
            }

            SetTerrain(heights, size);
        }

        public void Dispose()
        {
            if (Ground != Entity.Null)
            {
                var grid = EntityManager.GetComponentData<NavGrid>(Ground);
                if (grid.Terrain.IsCreated)
                {
                    grid.Terrain.Blob.Dispose();
                }

                grid.Dispose();
            }

            World.Dispose();
        }
    }
}
