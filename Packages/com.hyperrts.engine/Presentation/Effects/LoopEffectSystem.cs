using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Streams each drawn entity's <see cref="LoopEffect"/>s at their sockets while in range (half range on Low):
    /// gated by health, and faded out once a timed loop's duration since the entity was first seen runs out or as
    /// <see cref="Decay"/> remains sink.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoopEffectSystem : SystemBase
    {
        private readonly EffectStreams _streams = new();
        private readonly Dictionary<Entity, double> _firstSeen = new();
        private readonly List<Entity> _gone = new();
        private EffectPool _pool;
        private Camera _camera;
        private EntityQuery _loops;
        private int _loopsVersion;
        private float _reach;
        private float _deltaTime;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _loops = GetEntityQuery(ComponentType.ReadOnly<LoopEffect>());
            RequireForUpdate(_loops);
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _camera = EffectRange.Main(_camera);
            var now = SystemAPI.Time.ElapsedTime;
            _deltaTime = SystemAPI.Time.DeltaTime;
            _reach = EffectDetail.Level == EffectDetailLevel.Low ? 0.5f : 1f;
            var health = SystemAPI.GetComponentLookup<Health>(true);
            var decays = SystemAPI.GetComponentLookup<Decay>(true);
            _streams.BeginFrame();
            foreach (var (loops, place, entity) in SystemAPI.Query<DynamicBuffer<LoopEffect>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
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
            Prune();
        }

        private void Stream(Entity entity, int index, in LoopEffect loop, in LocalToWorld place, float health,
            float age, float standing)
        {
            var strength = LoopFade.Strength(age, loop.Duration) * standing;
            var gated = loop.AboveHealth > 0f && health <= loop.AboveHealth;
            if (strength <= 0f || gated || !loop.Rig.IsValid())
            {
                return;
            }

            var rig = _pool.Get(loop.Rig.Value);
            Vector3 socket = math.transform(place.Value, loop.Socket);
            if (rig == null || !EffectRange.InRange(_camera, rig, socket, _reach))
            {
                return;
            }

            var state = _streams.Step(entity, index, socket);
            rig.Stream(state.From, state.To, place.Rotation, _deltaTime, strength,
                EffectDetail.Density, ref state.Carry);
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
