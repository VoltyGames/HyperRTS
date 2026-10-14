using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Audio;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Plays the sounds of the frame's <see cref="CueEvent"/>s from a pool of audio sources: random clip, volume and pitch, 3D at
    /// the event position. Skips what the local player can't hear (fog, other players' voices) and 3D sounds out of
    /// the cue's range of the <see cref="AudioListener"/>.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SoundPlaybackSystem : SystemBase
    {
        private const int Voices = 32;

        private readonly SoundPool _pool = new(Voices);
        private TypeCatalog<List<(CueSlot Slot, SoundCue Cue)>> _cues;
        private EntityQuery _fog;
        private GameObject _root;
        private AudioSource[] _sources;
        private AudioListener _listener;

        protected override void OnCreate()
        {
            _cues = new TypeCatalog<List<(CueSlot Slot, SoundCue Cue)>>(SystemAPI.QueryBuilder()
                .WithAll<EntityInfo, EntitySound>().WithOptions(EntityQueryOptions.IncludePrefab).Build(), ReadCues);
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            RequireForUpdate<CueQueue>();
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
            var sounds = SystemAPI.GetSingletonBuffer<CueEvent>(true);
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

            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            for (var i = 0; i < sounds.Length; i++)
            {
                var sound = sounds[i];
                if (CueRules.IsVisible(sound, view.Viewer, fog, relations))
                {
                    Play(FindCue(registry, sound.TypeId, sound.Slot), sound.Position);
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

        /// <summary>The slot's cue among the type's, or null.</summary>
        private SoundCue FindCue(in PrefabRegistry registry, int typeId, CueSlot slot)
        {
            var cues = _cues.Find(EntityManager, registry, typeId);
            if (cues == null)
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

        private static List<(CueSlot Slot, SoundCue Cue)> ReadCues(EntityManager entityManager, Entity source)
        {
            var cues = new List<(CueSlot, SoundCue)>();
            foreach (var sound in entityManager.GetBuffer<EntitySound>(source, true))
            {
                cues.Add((sound.Slot, sound.Cue.Value));
            }

            return cues;
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
