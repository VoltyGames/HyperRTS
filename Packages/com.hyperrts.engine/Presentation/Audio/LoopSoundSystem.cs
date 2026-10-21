using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Plays each shown entity's <see cref="EntityLoopSound"/>s from replicated state, attached to the entity in 3D:
    /// engines by speed, rotors by flight, turrets while they turn, work and ambient hums. Fogged, dead and distant
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
        private EntityQuery _loops;
        private int _loopsVersion;
        private GameObject _root;
        private AudioSource[] _sources;
        private AudioListener _listener;

        protected override void OnCreate()
        {
            _loops = GetEntityQuery(ComponentType.ReadOnly<EntityLoopSound>());
            RequireForUpdate(_loops);
        }

        // The sources are DontSave, so they outlive scene reloads and must go with the world.
        protected override void OnDestroy()
        {
            if (_root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_root);
            }
            else
            {
                Object.DestroyImmediate(_root);
            }
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            if (_listener == null)
            {
                _listener = Object.FindAnyObjectByType<AudioListener>();
            }

            _mixer.Begin();
            if (_listener != null)
            {
                Offer(_listener.transform.position);
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
            var sites = SystemAPI.GetComponentLookup<ConstructionProgress>(true);
            var queues = SystemAPI.GetBufferLookup<ProductionQueueItem>(true);
            var poses = SystemAPI.GetBufferLookup<WeaponPose>(true);
            foreach (var (loops, tuning, place, entity) in SystemAPI
                         .Query<DynamicBuffer<EntityLoopSound>, RefRO<LoopSoundTuning>, RefRO<LocalToWorld>>()
                         .WithNone<DisableRendering, FogHidden, Dead>().WithEntityAccess())
            {
                var docked = docks.HasComponent(entity) && docks.IsComponentEnabled(entity);
                var flying = flights.HasComponent(entity) && !docked;
                var building = sites.HasComponent(entity) && sites.IsComponentEnabled(entity);
                var producing = queues.TryGetBuffer(entity, out var queue) && !queue.IsEmpty;
                _states.TryGetValue(entity, out var state);
                state.Step(place.ValueRO.Position, flying, Aim(poses, entity), tuning.ValueRO.SpoolTime, now, deltaTime);
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
            var sources = Sources();
            foreach (var released in _mixer.Released)
            {
                sources[released].Stop();
            }

            foreach (var (loop, index, claimed) in _mixer.Winners)
            {
                var source = sources[index];
                if (claimed)
                {
                    Claim(source, index, loop.Cue);
                }

                source.volume = _baseVolume[index] * loop.Level;
                source.pitch = _basePitch[index] * loop.PitchScale;
                source.transform.position = loop.Position;
            }
        }

        /// <summary>Starts the cue at a random point, so loops of one type never play in step.</summary>
        private void Claim(AudioSource source, int index, SoundCue cue)
        {
            _baseVolume[index] = Random.Range(cue.volume.x, cue.volume.y);
            _basePitch[index] = Random.Range(cue.pitch.x, cue.pitch.y);
            source.Stop();
            source.clip = cue.clips[Random.Range(0, cue.clips.Length)];
            source.priority = cue.priority;
            source.outputAudioMixerGroup = cue.mixerGroup;
            source.spatialBlend = cue.spatial ? 1f : 0f;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            if (source.clip == null)
            {
                return;
            }

            source.time = Random.Range(0f, source.clip.length);
            source.Play();
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

        private AudioSource[] Sources()
        {
            if (_root != null)
            {
                return _sources;
            }

            _root = new GameObject("HyperRTS Loops") { hideFlags = HideFlags.DontSave };
            _sources = new AudioSource[Voices];
            for (var i = 0; i < _sources.Length; i++)
            {
                var voice = new GameObject($"Loop {i}") { hideFlags = HideFlags.DontSave };
                voice.transform.SetParent(_root.transform);
                _sources[i] = voice.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].loop = true;
                _sources[i].rolloffMode = AudioRolloffMode.Linear;
            }

            return _sources;
        }
    }
}
