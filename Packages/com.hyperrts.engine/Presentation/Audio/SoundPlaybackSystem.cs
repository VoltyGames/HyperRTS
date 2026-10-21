using HyperRTS.Core;
using HyperRTS.Presentation.Common;
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

            var source = _voices.Load(voice, cue, clip, pitch);
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
