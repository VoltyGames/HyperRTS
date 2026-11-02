using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Effects.Lights;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Combat.Death;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Effects.Streams
{
    /// <summary>
    /// Streams each drawn, finished entity's <see cref="LoopEffect"/>s at their sockets while in range (half range on Low):
    /// gated by health, and faded out once a timed loop's duration since the entity was first seen runs out or as
    /// <see cref="Decay"/> remains are buried. Hands the streamed loops to <see cref="LoopLights"/> and
    /// <see cref="SmokeShadows"/>.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoopEffectSystem : SystemBase
    {
        private EffectStreams _streams;
        private readonly Dictionary<Entity, double> _firstSeen = new();
        private readonly List<Entity> _gone = new();
        private readonly SmokeShadows _smoke = new();
        private GameObject _lightsRoot;
        private LoopLights _lights;
        private EffectPool _pool;
        private EntityQuery _loops;
        private int _loopsVersion;
        private float _reach;
        private float _deltaTime;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _streams = new EffectStreams(_pool);
            _loops = GetEntityQuery(ComponentType.ReadOnly<LoopEffect>());
            RequireForUpdate(_loops);
            _lightsRoot = new GameObject("HyperRTS Loop Lights") { hideFlags = HideFlags.DontSave };
            _lights = new LoopLights(_lightsRoot.transform);
        }

        protected override void OnDestroy() => CoreUtils.Destroy(_lightsRoot);

        // The last loop is gone, so put out its light and shadow.
        protected override void OnStopRunning() => ShowLightsAndShadows(0f);

        protected override void OnUpdate()
        {
            CompleteDependency();
            var now = SystemAPI.Time.ElapsedTime;
            _deltaTime = SystemAPI.Time.DeltaTime;
            _reach = PresentationQuality.Current.Effects == DetailLevel.Low ? 0.5f : 1f;
            var health = SystemAPI.GetComponentLookup<Health>(true);
            var decays = SystemAPI.GetComponentLookup<Decay>(true);
            _streams.BeginFrame();
            foreach (var (loops, place, entity) in SystemAPI.Query<DynamicBuffer<LoopEffect>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside, ConstructionProgress>().WithEntityAccess())
            {
                if (!_firstSeen.TryGetValue(entity, out var seen))
                {
                    _firstSeen[entity] = seen = now;
                }

                var fraction = health.TryGetComponent(entity, out var hp) ? hp.Fraction : 1f;
                var standing = decays.TryGetComponent(entity, out var decay) ? 1f - decay.DespawnProgress : 1f;
                for (var i = 0; i < loops.Length; i++)
                {
                    Stream(new LoopSource(entity, place.ValueRO, fraction, (float)(now - seen), standing), i, loops[i]);
                }
            }

            _streams.EndFrame();
            ShowLightsAndShadows((float)now);
            Prune();
        }

        private void Stream(in LoopSource source, int index, in LoopEffect loop)
        {
            var (entity, place, health, age, standing) = (source.Entity, source.Place, source.Health, source.Age, source.Standing);
            var strength = LoopFade.Strength(age, loop.Duration) * standing;
            var belowHealth = loop.AboveHealth > 0f && health <= loop.AboveHealth;
            if (strength <= 0f || belowHealth || !loop.Rig.IsValid())
            {
                return;
            }

            var socket = math.transform(place.Value, loop.Socket);
            var state = _streams.Step(loop.Rig.Value, entity, index, socket, _reach);
            if (state == null)
            {
                return;
            }

            state.EmitLoop(place.Rotation, _deltaTime, strength);
            _lights.Offers.Add(state.Rig.LoopLight, socket, strength, state.Key, age);
            // The shadow follows the smoke actually emitted, which density thins.
            _smoke.Offers.Add(state.Rig.SmokeShadow, socket, strength * EffectDetail.Density, state.Key, age);
        }

        /// <summary>The entity a loop streams from, with its health fraction, seconds seen and how much of it still stands.</summary>
        private readonly struct LoopSource
        {
            public readonly Entity Entity;
            public readonly LocalToWorld Place;
            public readonly float Health;
            public readonly float Age;
            public readonly float Standing;

            public LoopSource(Entity entity, in LocalToWorld place, float health, float age, float standing)
            {
                Entity = entity;
                Place = place;
                Health = health;
                Age = age;
                Standing = standing;
            }
        }

        private void ShowLightsAndShadows(float time)
        {
            var eye = _pool.Eye;
            _lights.Show(eye, PresentationQuality.Current.Effects, time);
            _smoke.Show(eye, PresentationQuality.Current.Effects);
        }

        /// <summary>Forgets when entities that no longer exist were first seen, once loop entities came or went.</summary>
        private void Prune()
        {
            var version = _loops.GetCombinedComponentOrderVersion(includeEntityType: false);
            if (version == _loopsVersion)
            {
                return;
            }

            _loopsVersion = version;
            _gone.Clear();
            foreach (var entity in _firstSeen.Keys)
            {
                if (!EntityManager.Exists(entity))
                {
                    _gone.Add(entity);
                }
            }

            foreach (var entity in _gone)
            {
                _firstSeen.Remove(entity);
            }
        }
    }
}
