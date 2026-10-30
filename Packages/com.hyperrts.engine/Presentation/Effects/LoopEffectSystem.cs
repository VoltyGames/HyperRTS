using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Effects
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
        private readonly EffectStreams _streams = new();
        private readonly Dictionary<Entity, double> _firstSeen = new();
        private readonly List<Entity> _gone = new();
        private readonly SmokeShadows _smoke = new();
        private GameObject _lightsRoot;
        private LoopLights _lights;
        private Camera _camera;
        private EffectPool _pool;
        private EntityQuery _loops;
        private int _loopsVersion;
        private float _reach;
        private float _deltaTime;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
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
            _reach = EffectDetail.Level == DetailLevel.Low ? 0.5f : 1f;
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
                    Stream(entity, i, loops[i], place.ValueRO, fraction, (float)(now - seen), standing);
                }
            }

            _streams.EndFrame();
            ShowLightsAndShadows((float)now);
            Prune();
        }

        private void Stream(Entity entity, int index, in LoopEffect loop, in LocalToWorld place, float health,
            float age, float standing)
        {
            var strength = LoopFade.Strength(age, loop.Duration) * standing;
            var belowHealth = loop.AboveHealth > 0f && health <= loop.AboveHealth;
            if (strength <= 0f || belowHealth || !loop.Rig.IsValid())
            {
                return;
            }

            var socket = math.transform(place.Value, loop.Socket);
            var state = _pool.Step(_streams, loop.Rig.Value, entity, index, socket, _reach);
            if (state == null)
            {
                return;
            }

            state.Emit(state.Rig.LoopRotation(place.Rotation, state.Key), _deltaTime, strength,
                state.Rig.LoopScale(state.Key));
            _lights.Offers.Add(state.Rig, socket, strength, state.Key, age);
            // The shadow follows the smoke actually emitted, which density thins.
            _smoke.Offers.Add(state.Rig, socket, strength * EffectDetail.Density, state.Key, age);
        }

        private void ShowLightsAndShadows(float time)
        {
            // Without a camera nothing streams, so there are no offers to sort by the eye.
            _camera = EffectRange.Main(_camera);
            var eye = _camera != null ? _camera.transform.position : Vector3.zero;
            _lights.Show(eye, EffectDetail.Level, time);
            _smoke.Show(eye, EffectDetail.Level);
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
