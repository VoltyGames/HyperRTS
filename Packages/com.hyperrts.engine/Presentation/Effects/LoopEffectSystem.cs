using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Streams each drawn entity's <see cref="LoopEffect"/>s at their sockets while in range (half range on Low):
    /// gated by health, and faded out once a timed loop's duration since the entity was first seen runs out.
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

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<LoopEffect>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _camera = EffectRange.Main(_camera);
            var now = SystemAPI.Time.ElapsedTime;
            var health = SystemAPI.GetComponentLookup<Health>(true);
            _streams.BeginFrame();
            foreach (var (loops, place, entity) in SystemAPI.Query<DynamicBuffer<LoopEffect>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering>().WithEntityAccess())
            {
                if (!_firstSeen.TryGetValue(entity, out var seen))
                {
                    _firstSeen[entity] = seen = now;
                }

                var fraction = health.TryGetComponent(entity, out var hp) ? hp.Fraction : 1f;
                for (var i = 0; i < loops.Length; i++)
                {
                    Stream(entity, i, loops[i], place.ValueRO, fraction, (float)(now - seen));
                }
            }

            _streams.EndFrame();
            Prune();
        }

        private void Stream(Entity entity, int index, in LoopEffect loop, in LocalToWorld place, float health,
            float age)
        {
            var strength = LoopFade.Strength(age, loop.Duration);
            var gated = loop.AboveHealth > 0f && health <= loop.AboveHealth;
            if (strength <= 0f || gated || !loop.Rig.IsValid())
            {
                return;
            }

            var rig = _pool.Get(loop.Rig.Value);
            Vector3 socket = math.transform(place.Value, loop.Socket);
            var reach = EffectDetail.Level == EffectDetailLevel.Low ? 0.5f : 1f;
            if (rig == null || !EffectRange.InRange(_camera, rig, socket, reach))
            {
                return;
            }

            var state = _streams.Step(entity, index, socket);
            rig.Stream(state.From, state.To, place.Rotation, SystemAPI.Time.DeltaTime, strength,
                EffectPoolSystem.Density, ref state.Carry);
        }

        /// <summary>Forgets when entities that no longer exist were first seen.</summary>
        private void Prune()
        {
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
