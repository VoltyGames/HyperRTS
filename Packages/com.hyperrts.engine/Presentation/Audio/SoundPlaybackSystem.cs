using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Plays the frame's <see cref="SoundEvent"/>s from a pool of audio sources: random clip, volume and pitch, 3D at
    /// the event position. Skips what the local player can't hear (fog, other players' voices) and 3D sounds out of
    /// the cue's range of the <see cref="AudioListener"/>.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SoundPlaybackSystem : SystemBase
    {
        private const int Voices = 32;

        private readonly SoundPool _pool = new(Voices);
        private readonly Dictionary<int, List<(SoundSlot Slot, SoundCue Cue)>> _cues = new();
        private readonly HashSet<int> _missing = new();
        private int _typedVersion;
        private EntityQuery _typed;
        private EntityQuery _fog;
        private GameObject _root;
        private AudioSource[] _sources;
        private AudioListener _listener;

        protected override void OnCreate()
        {
            _typed = SystemAPI.QueryBuilder().WithAll<EntityInfo, EntitySound>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            RequireForUpdate<SoundQueue>();
            RequireForUpdate<PrefabRegistry>();
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
            var sounds = SystemAPI.GetSingletonBuffer<SoundEvent>(true);
            if (sounds.IsEmpty)
            {
                return;
            }

            var view = SystemAPI.TryGetSingleton(out LocalFogView local) ? local : default;
            SystemAPI.TryGetSingleton(out FactionRelations relations);
            _fog.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            if (_listener == null)
            {
                _listener = Object.FindAnyObjectByType<AudioListener>();
            }

            for (var i = 0; i < sounds.Length; i++)
            {
                var sound = sounds[i];
                if (SoundRules.IsAudible(sound, view.Viewer, fog, relations))
                {
                    Play(CueOf(sound.TypeId, sound.Slot), sound.Position);
                }
            }
        }

        private void Play(SoundCue cue, Vector3 position)
        {
            if (cue == null || cue.clips.Length == 0 || IsOutOfRange(cue, position))
            {
                return;
            }

            var clip = cue.clips[Random.Range(0, cue.clips.Length)];
            var pitch = Random.Range(cue.pitch.x, cue.pitch.y);
            if (clip == null || pitch <= 0f)
            {
                return;
            }

            var voice = _pool.Acquire(cue, AudioSettings.dspTime, clip.length / pitch);
            if (voice < 0)
            {
                return;
            }

            var source = Sources()[voice];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Random.Range(cue.volume.x, cue.volume.y);
            source.priority = cue.priority;
            source.outputAudioMixerGroup = cue.mixerGroup;
            source.spatialBlend = cue.spatial ? 1f : 0f;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            source.transform.position = position;
            source.Play();
        }

        private bool IsOutOfRange(SoundCue cue, Vector3 position)
        {
            if (!cue.spatial || _listener == null)
            {
                return false;
            }

            return (position - _listener.transform.position).sqrMagnitude > cue.maxDistance * cue.maxDistance;
        }

        /// <summary>
        /// Cues come from any prefab or instance of the type, cached per type: a dead unit's own entity may be gone,
        /// and on a client the event names a type the server resolved.
        /// </summary>
        private SoundCue CueOf(int typeId, SoundSlot slot)
        {
            if (!_cues.TryGetValue(typeId, out var cues) && !TryLoad(typeId, out cues))
            {
                return null;
            }

            foreach (var (cueSlot, cue) in cues)
            {
                if (cueSlot == slot)
                {
                    return cue;
                }
            }

            return null;
        }

        /// <summary>
        /// Misses are cached too, until an entity with sounds is created or destroyed (a prefab streaming in).
        /// </summary>
        private bool TryLoad(int typeId, out List<(SoundSlot Slot, SoundCue Cue)> cues)
        {
            cues = null;
            var version = _typed.GetCombinedComponentOrderVersion(includeEntityType: true);
            if (version != _typedVersion)
            {
                _typedVersion = version;
                _missing.Clear();
            }

            var source = _missing.Contains(typeId) ? Entity.Null : SourceOf(typeId);
            if (source == Entity.Null)
            {
                _missing.Add(typeId);
                return false;
            }

            cues = new List<(SoundSlot, SoundCue)>();
            foreach (var sound in EntityManager.GetBuffer<EntitySound>(source, true))
            {
                cues.Add((sound.Slot, sound.Cue.Value));
            }

            _cues[typeId] = cues;
            return true;
        }

        /// <summary>The type's prefab, else any instance: a SubScene-placed entity may have no prefab loaded.</summary>
        private Entity SourceOf(int typeId)
        {
            var prefab = SystemAPI.GetSingleton<PrefabRegistry>().Find(typeId);
            if (EntityManager.HasBuffer<EntitySound>(prefab))
            {
                return prefab;
            }

            using var entities = _typed.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (EntityManager.GetComponentData<EntityInfo>(entity).TypeId == typeId)
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private AudioSource[] Sources()
        {
            if (_root != null)
            {
                return _sources;
            }

            _root = new GameObject("HyperRTS Sounds") { hideFlags = HideFlags.DontSave };
            _sources = new AudioSource[_pool.Capacity];
            for (var i = 0; i < _sources.Length; i++)
            {
                var voice = new GameObject($"Voice {i}") { hideFlags = HideFlags.DontSave };
                voice.transform.SetParent(_root.transform);
                _sources[i] = voice.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].rolloffMode = AudioRolloffMode.Linear;
            }

            return _sources;
        }
    }
}
