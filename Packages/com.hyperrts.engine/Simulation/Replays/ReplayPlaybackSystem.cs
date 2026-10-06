using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// Shows a replay without simulating: takes over the scene's entities (same type, nearest position), then each
    /// frame spawns, destroys and interpolates entities to the recorded state at <see cref="ReplayPlayback.Time"/>.
    /// Seeking rebuilds from the nearest keyframe at or before the time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ReplaySystemGroup))]
    public partial struct ReplayPlaybackSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Faction> _factions;
        private ComponentLookup<Health> _health;
        private ComponentLookup<ConstructionProgress> _construction;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>();
            _factions = state.GetComponentLookup<Faction>();
            _health = state.GetComponentLookup<Health>();
            _construction = state.GetComponentLookup<ConstructionProgress>();
            state.RequireForUpdate<ReplayPlaybackState>();
            state.RequireForUpdate<MapSettings>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton(out ReplayPlaybackState playback))
            {
                playback.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entity = SystemAPI.GetSingletonEntity<ReplayPlaybackState>();
            var data = SystemAPI.GetComponent<ReplayPlaybackState>(entity);
            var clock = SystemAPI.GetComponent<ReplayPlayback>(entity);
            clock.Advance(SystemAPI.Time.DeltaTime);

            var index = data.Stream.FrameAt(clock.Time);
            if (data.Index < 0)
            {
                data.Seek(index);
                TakeOver(ref state, ref data);
            }
            else if (index != data.Index)
            {
                data.Seek(index);
                Sync(ref state, ref data);
            }

            Show(ref state, in data, clock.Time);
            SystemAPI.SetComponent(entity, data);
            SystemAPI.SetComponent(entity, clock);
        }

        /// <summary>Claims the scene's entities for the recorded ones, removes the rest and spawns what is missing.</summary>
        private void TakeOver(ref SystemState state, ref ReplayPlaybackState data)
        {
            var prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            PrefabLookup.ByTypeId(prefabs, data.Prefabs);

            var scene = SystemAPI.QueryBuilder().WithAll<EntityInfo, Faction, LocalTransform>().Build();
            var unclaimed = new NativeList<Entity>(Allocator.Temp);
            ReplaySceneMatch.Match(data.From, scene.ToEntityArray(Allocator.Temp),
                scene.ToComponentDataArray<EntityInfo>(Allocator.Temp),
                scene.ToComponentDataArray<LocalTransform>(Allocator.Temp), data.Live, unclaimed);
            state.EntityManager.DestroyEntity(unclaimed.AsArray());
            Sync(ref state, ref data);
        }

        /// <summary>Destroys entities whose key is gone and spawns the keys that have none.</summary>
        private static void Sync(ref SystemState state, ref ReplayPlaybackState data)
        {
            var staleKeys = new NativeList<int>(Allocator.Temp);
            var stale = new NativeList<Entity>(Allocator.Temp);
            foreach (var pair in data.Live)
            {
                if (data.From.ContainsKey(pair.Key))
                {
                    continue;
                }

                staleKeys.Add(pair.Key);
                if (state.EntityManager.Exists(pair.Value))
                {
                    stale.Add(pair.Value);
                }
            }

            foreach (var key in staleKeys)
            {
                data.Live.Remove(key);
            }

            state.EntityManager.DestroyEntity(stale.AsArray());

            foreach (var pair in data.From)
            {
                if (!data.Live.ContainsKey(pair.Key) && TrySpawn(ref state, ref data, pair.Value.TypeId, out var spawned))
                {
                    data.Live[pair.Key] = spawned;
                }
            }
        }

        private static bool TrySpawn(ref SystemState state, ref ReplayPlaybackState data, int typeId, out Entity spawned)
        {
            spawned = Entity.Null;
            if (data.Prefabs.TryGetValue(typeId, out var prefab))
            {
                spawned = state.EntityManager.Instantiate(prefab);
                return true;
            }

            if (data.MissingTypes.Add(typeId))
            {
                Debug.LogWarning($"Replay: no baked prefab for entity type {typeId}; its entities are not shown.");
            }

            return false;
        }

        private void Show(ref SystemState state, in ReplayPlaybackState data, float time)
        {
            _transforms.Update(ref state);
            _factions.Update(ref state);
            _health.Update(ref state);
            _construction.Update(ref state);
            state.CompleteDependency();

            var blend = Blend(data, time);
            foreach (var pair in data.From)
            {
                if (data.Live.TryGetValue(pair.Key, out var entity))
                {
                    var next = data.To.TryGetValue(pair.Key, out var to) ? to : pair.Value;
                    ShowEntity(entity, pair.Value, next, blend);
                }
            }
        }

        private static float Blend(in ReplayPlaybackState data, float time)
        {
            var frames = data.Stream.Frames;
            var start = frames[data.Index].Time;
            var end = data.Index + 1 < frames.Length ? frames[data.Index + 1].Time : start;
            return end > start ? math.saturate((time - start) / (end - start)) : 0f;
        }

        private void ShowEntity(Entity entity, in ReplayEntity from, in ReplayEntity to, float blend)
        {
            if (_transforms.TryGetComponent(entity, out var transform))
            {
                _transforms[entity] = from.Interpolate(to, blend, transform.Scale);
            }

            if (_factions.TryGetComponent(entity, out var faction) && faction.Value != from.Faction)
            {
                _factions[entity] = new Faction { Value = from.Faction };
            }

            if (_health.TryGetComponent(entity, out var health))
            {
                health.Current = health.Max * from.HealthFraction;
                _health[entity] = health;
            }

            if (_construction.HasComponent(entity))
            {
                _construction[entity] = new ConstructionProgress { Value = from.ConstructionValue };
                _construction.SetComponentEnabled(entity, from.UnderConstruction);
            }
        }
    }
}
