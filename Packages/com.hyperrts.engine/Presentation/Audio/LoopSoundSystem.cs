using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Plays each shown entity's <see cref="EntityLoopSound"/>s from replicated state, attached to the entity in 3D:
    /// engines by speed, rotors by the rig's <see cref="RotorSpin"/>, turrets while they turn, work and ambient hums. Fogged, dead and distant
    /// entities fall silent, and <see cref="LoopMixer"/> keeps the nearest within each cue's instance limit.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LoopSoundSystem : SystemBase
    {
        private const int Voices = 24;

        private readonly LoopMixer _mixer = new(Voices);
        private readonly Dictionary<Entity, LoopSoundState> _states = new();
        private readonly List<Entity> _gone = new();
        private readonly float[] _baseVolume = new float[Voices];
        private readonly float[] _basePitch = new float[Voices];
        private readonly VoiceBank _voices = new("HyperRTS Loops", Voices, loop: true);
        private readonly ListenerLookup _listener = new();
        private EntityQuery _loops;
        private int _loopsVersion;

        protected override void OnCreate()
        {
            _loops = GetEntityQuery(ComponentType.ReadOnly<EntityLoopSound>());
            RequireForUpdate(_loops);
        }

        protected override void OnDestroy() => _voices.Dispose();

        protected override void OnUpdate()
        {
            CompleteDependency();
            _mixer.Begin();
            var listener = _listener.Find();
            if (listener != null)
            {
                Offer(listener.transform.position);
            }

            _mixer.Resolve();
            Play();
            Prune();
        }

        private void Offer(Vector3 listener)
        {
            var now = SystemAPI.Time.ElapsedTime;
            var deltaTime = SystemAPI.Time.DeltaTime;
            var flights = SystemAPI.GetComponentLookup<Flight>(true);
            var docks = SystemAPI.GetComponentLookup<Docked>(true);
            var spins = SystemAPI.GetComponentLookup<RotorSpin>(true);
            var sites = SystemAPI.GetComponentLookup<ConstructionProgress>(true);
            var queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(true);
            var poses = SystemAPI.GetBufferLookup<WeaponPose>(true);
            foreach (var (loops, tuning, place, entity) in SystemAPI
                         .Query<DynamicBuffer<EntityLoopSound>, RefRO<LoopSoundTuning>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside, Dead>().WithEntityAccess())
            {
                var flying = flights.HasComponent(entity) && !docks.HasEnabled(entity);
                // A unit with no rig rotor to follow plays its rotor full while flying.
                var rotor = flying ? 1f : 0f;
                if (spins.TryGetComponent(entity, out var spin))
                {
                    rotor = spin.Amount;
                }

                var building = sites.HasEnabled(entity);
                var producing = queues.TryGetBuffer(entity, out var queue) && !queue.IsEmpty;
                _states.TryGetValue(entity, out var state);
                state.Step(place.ValueRO.Position, rotor, Aim(poses, entity), now, deltaTime);
                _states[entity] = state;

                Vector3 position = place.ValueRO.Position;
                var distanceSq = (position - listener).sqrMagnitude;
                foreach (var loop in loops)
                {
                    _mixer.Offer(new LoopCandidate
                    {
                        Entity = entity, Slot = loop.Slot, Cue = loop.Cue.Value, Position = position,
                        DistanceSq = distanceSq, Level = state.Level(loop.Slot, tuning.ValueRO, building || producing),
                        PitchScale = state.PitchScale(loop.Slot, tuning.ValueRO),
                    });
                }
            }
        }

        /// <summary>Every weapon's heading folded into one value, so any turn changes it.</summary>
        private static uint Aim(BufferLookup<WeaponPose> poses, Entity entity)
        {
            if (!poses.TryGetBuffer(entity, out var weapons))
            {
                return 0;
            }

            var aim = 0u;
            foreach (var pose in weapons)
            {
                aim = aim * 257u + pose.Yaw;
            }

            return aim;
        }

        private void Play()
        {
            foreach (var released in _mixer.Released)
            {
                _voices[released].Stop();
            }

            foreach (var (loop, index, claimed) in _mixer.Winners)
            {
                var source = claimed ? Claim(index, loop.Cue) : _voices[index];

                source.volume = _baseVolume[index] * loop.Level;
                source.pitch = _basePitch[index] * loop.PitchScale;
                source.transform.position = loop.Position;
            }
        }

        /// <summary>Starts the cue at a random point, so loops of one type never play in step.</summary>
        private AudioSource Claim(int index, SoundCue cue)
        {
            var clip = cue.clips[Random.Range(0, cue.clips.Length)];
            var source = _voices.Load(index, cue, clip, Random.Range(cue.pitch.x, cue.pitch.y));
            _baseVolume[index] = source.volume;
            _basePitch[index] = source.pitch;
            if (clip == null)
            {
                return source;
            }

            source.time = Random.Range(0f, clip.length);
            source.Play();
            return source;
        }

        /// <summary>Forgets the state of entities that no longer exist, once loop entities came or went.</summary>
        private void Prune()
        {
            var version = _loops.GetCombinedComponentOrderVersion(includeEntityType: false);
            if (version == _loopsVersion)
            {
                return;
            }

            _loopsVersion = version;
            _gone.Clear();
            foreach (var entity in _states.Keys)
            {
                if (!EntityManager.Exists(entity))
                {
                    _gone.Add(entity);
                }
            }

            foreach (var entity in _gone)
            {
                _states.Remove(entity);
            }
        }
    }
}
