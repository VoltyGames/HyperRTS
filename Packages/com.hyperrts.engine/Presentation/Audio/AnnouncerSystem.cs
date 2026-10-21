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
    /// Speaks the local player's announcer alerts from the frame's cues, one at a time in 2D, through the
    /// <see cref="AnnouncerSet"/> its side's <see cref="AnnouncerVoice"/> names.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AnnouncerSystem : SystemBase
    {
        private readonly AnnouncerQueue _queue = new();
        private CueVisibility _visibility;
        private EntityQuery _voices;
        private EntityQuery _localSide;
        private AudioSource _source;

        protected override void OnCreate()
        {
            _visibility = new CueVisibility(ref CheckedStateRef);
            _voices = GetEntityQuery(ComponentType.ReadOnly<AnnouncerVoice>());
            _localSide = GetEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<PlayerSide>());
            RequireForUpdate<CueQueue>();
            RequireForUpdate<LocalFogView>();
            RequireForUpdate(_voices);
            RequireForUpdate(_localSide);
        }

        // The source is DontSave, so it outlives scene reloads and must go with the world.
        protected override void OnDestroy()
        {
            if (_source == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_source.gameObject);
            }
            else
            {
                Object.DestroyImmediate(_source.gameObject);
            }
        }

        protected override void OnUpdate()
        {
            var set = LocalSet();
            if (set == null)
            {
                return;
            }

            var now = SystemAPI.Time.ElapsedTime;
            _visibility.Begin();
            foreach (var cue in SystemAPI.GetSingletonBuffer<CueEvent>(true))
            {
                if (CueRules.IsAnnouncement(cue.Slot) && _visibility.IsSeen(cue))
                {
                    _queue.Offer(set, cue.Slot, now);
                }
            }

            var speaking = _source != null && _source.isPlaying;
            if (!speaking && _queue.Count > 0)
            {
                Speak(_queue.Next(now, set.maxWait));
            }
        }

        /// <summary>The set the local player's side hears; null when its side has none.</summary>
        private AnnouncerSet LocalSet()
        {
            var side = _localSide.GetSingleton<PlayerSide>().Value;
            var voices = EntityManager.GetBuffer<AnnouncerVoice>(_voices.GetSingletonEntity(), true);
            foreach (var voice in voices)
            {
                if (voice.Side == side)
                {
                    return voice.Set.Value;
                }
            }

            return null;
        }

        private void Speak(SoundCue cue)
        {
            if (cue == null || cue.clips.Length == 0)
            {
                return;
            }

            var source = Source();
            source.clip = cue.clips[Random.Range(0, cue.clips.Length)];
            source.volume = Random.Range(cue.volume.x, cue.volume.y);
            source.pitch = Random.Range(cue.pitch.x, cue.pitch.y);
            source.priority = cue.priority;
            source.outputAudioMixerGroup = cue.mixerGroup;
            source.Play();
        }

        private AudioSource Source()
        {
            if (_source != null)
            {
                return _source;
            }

            var root = new GameObject("HyperRTS Announcer") { hideFlags = HideFlags.DontSave };
            _source = root.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            return _source;
        }
    }
}
