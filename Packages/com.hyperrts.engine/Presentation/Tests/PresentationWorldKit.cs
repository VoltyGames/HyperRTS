using System;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.GameEntities;
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

        public Entity AddPlayer(byte faction, float4 color = default, bool local = false, byte side = 0)
        {
            var player = EntityManager.CreateEntity();
            var writer = new EntityManagerWriter(EntityManager, player);
            PlayerSetup.Add(ref writer, faction, $"P{faction}", color, populationCap: 0);
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

        public void Dispose() => World.Dispose();
    }
}
