using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Plays the frame's <see cref="CueEvent"/>s from a pool of audio sources, skipping what the local player can't hear
    /// or would hear too faintly at the <see cref="AudioListener"/>, and drawing distant variations for far sounds.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SoundPlaybackSystem : SystemBase
    {
        private const int Voices = 32;

        private readonly SoundPool _pool = new(Voices);
        private TypeCatalog<EntitySound[]> _cues;
        private readonly VoiceBank _voices = new("HyperRTS Sounds", Voices);
        private readonly ListenerLookup _listeners = new();
        private CueVisibility _visibility;
        private AudioListener _listener;

        protected override void OnCreate()
        {
            _cues = new TypeCatalog<EntitySound[]>(SystemAPI.QueryBuilder()
                .WithAll<EntityInfo, EntitySound>().WithOptions(EntityQueryOptions.IncludePrefab).Build(), ReadCues);
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<CueQueue>();
            RequireForUpdate<LocalFogView>();
            RequireForUpdate<PrefabRegistry>();
        }

        protected override void OnDestroy() => _voices.Dispose();

        protected override void OnUpdate()
        {
            var sounds = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (sounds.IsEmpty)
            {
                return;
            }

            _visibility.Begin();
            _listener = _listeners.Find();

            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            for (var i = 0; i < sounds.Length; i++)
            {
                var sound = sounds[i];
                if (_visibility.IsSeen(sound))
                {
                    Play(FindCue(registry, sound), sound.Position);
                }
            }
        }

        private void Play(SoundCue cue, Vector3 position)
        {
            if (cue == null || cue.clips.Length == 0)
            {
                return;
            }

            var distance = ListenerDistance(cue, position);
            cue.Pick(cue.IsDistant(distance), out var clip, out var pitch);
            if (clip == null || pitch <= 0f)
            {
                return;
            }

            var volume = cue.PickVolume();
            var fade = cue.spatial ? DistanceCurves.Volume(distance, cue.minDistance, cue.maxDistance) : 1f;
            var loudness = volume * fade;
            var voice = _pool.Acquire(cue, loudness, AudioSettings.dspTime, clip.length / pitch);
            if (voice < 0)
            {
                return;
            }

            var source = _voices.Load(voice, cue, clip, pitch, volume);
            source.transform.position = position;
            source.Play();
        }

        /// <summary>How far a 3D cue is from the listener; 0 for a 2D cue or before the scene has a listener.</summary>
        private float ListenerDistance(SoundCue cue, Vector3 position)
        {
            if (!cue.spatial || _listener == null)
            {
                return 0f;
            }

            return Vector3.Distance(position, _listener.transform.position);
        }

        /// <summary>The cue for the event's slot and variant among its type's, or null.</summary>
        private SoundCue FindCue(in PrefabRegistry registry, in CueEvent cue)
        {
            var sounds = _cues.Find(EntityManager, registry, cue.TypeId);
            if (sounds == null)
            {
                return null;
            }

            foreach (var sound in sounds)
            {
                if (sound.Plays(cue.Slot, cue.Variant))
                {
                    return sound.Cue.Value;
                }
            }

            return null;
        }

        private static EntitySound[] ReadCues(EntityManager entityManager, Entity source) =>
            entityManager.GetBuffer<EntitySound>(source, true).AsNativeArray().ToArray();
    }
}
