using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
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
            Advance(ref clock, SystemAPI.Time.DeltaTime);

            var index = data.Stream.FrameAt(clock.Time);
            if (data.Index < 0)
            {
                MoveTo(ref data, index);
                TakeOver(ref state, ref data);
            }
            else if (index != data.Index)
            {
                MoveTo(ref data, index);
                Sync(ref state, ref data);
            }

            Show(ref state, in data, clock.Time);
            SystemAPI.SetComponent(entity, data);
            SystemAPI.SetComponent(entity, clock);
        }

        private static void Advance(ref ReplayPlayback clock, float deltaTime)
        {
            if (clock.Playing)
            {
                clock.Time += deltaTime * clock.Speed;
            }

            clock.Time = math.clamp(clock.Time, 0f, clock.Duration);
            if (clock.Time >= clock.Duration)
            {
                clock.Playing = false;
            }
        }

        /// <summary>Rebuilds from a keyframe when going back or past one, otherwise steps through the deltas.</summary>
        private static void MoveTo(ref ReplayPlaybackState data, int index)
        {
            var keyframe = data.Stream.KeyframeAtOrBefore(index);
            if (index < data.Index || keyframe > data.Index)
            {
                data.Index = keyframe;
                data.Stream.Apply(keyframe, data.From);
            }

            while (data.Index < index)
            {
                data.Index++;
                data.Stream.Apply(data.Index, data.From);
            }

            data.To.Clear();
            foreach (var pair in data.From)
            {
                data.To[pair.Key] = pair.Value;
            }

            if (index + 1 < data.Stream.Frames.Length)
            {
                data.Stream.Apply(index + 1, data.To);
            }
        }

        private void TakeOver(ref SystemState state, ref ReplayPlaybackState data)
        {
            var prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            PrefabLookup.ByTypeId(prefabs, data.Prefabs);

            // The viewer sees everything: drop fog tags left from before playback started.
            state.EntityManager.RemoveComponent<FogHidden>(SystemAPI.QueryBuilder().WithAll<FogHidden>().Build());
            if (SystemAPI.TryGetSingletonRW<LocalFogView>(out var view))
            {
                view.ValueRW.Active = false;
                view.ValueRW.Version++;
            }

            MatchScene(ref state, ref data);
            Sync(ref state, ref data);
        }

        /// <summary>Pairs recorded entities with the scene's (same type, nearest) and removes unrecorded ones.</summary>
        private void MatchScene(ref SystemState state, ref ReplayPlaybackState data)
        {
            var scene = SystemAPI.QueryBuilder().WithAll<EntityInfo, Faction, LocalTransform>().Build();
            var entities = scene.ToEntityArray(Allocator.Temp);
            var infos = scene.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            var transforms = scene.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var claimed = new NativeArray<bool>(entities.Length, Allocator.Temp);

            foreach (var pair in data.From)
            {
                var match = Nearest(pair.Value, infos, transforms, claimed);
                if (match >= 0)
                {
                    claimed[match] = true;
                    data.Live[pair.Key] = entities[match];
                }
            }

            for (var i = 0; i < entities.Length; i++)
            {
                if (!claimed[i])
                {
                    state.EntityManager.DestroyEntity(entities[i]);
                }
            }
        }

        private static int Nearest(in ReplayEntity recorded, NativeArray<EntityInfo> infos,
            NativeArray<LocalTransform> transforms, NativeArray<bool> claimed)
        {
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < infos.Length; i++)
            {
                if (claimed[i] || infos[i].TypeId != recorded.TypeId)
                {
                    continue;
                }

                var distance = math.distancesq(transforms[i].Position, recorded.Position);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Destroys entities whose key is gone and spawns the keys that have none.</summary>
        private static void Sync(ref SystemState state, ref ReplayPlaybackState data)
        {
            var stale = new NativeList<int>(Allocator.Temp);
            foreach (var pair in data.Live)
            {
                if (!data.From.ContainsKey(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                if (state.EntityManager.Exists(data.Live[key]))
                {
                    state.EntityManager.DestroyEntity(data.Live[key]);
                }

                data.Live.Remove(key);
            }

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
