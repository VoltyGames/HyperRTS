using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Sounds;
using HyperRTS.Simulation.Vision.Fog;
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
        private readonly VoiceBank _voice = new("HyperRTS Announcer", 1);
        private CueVisibility _visibility;
        private EntityQuery _voices;
        private EntityQuery _localSide;

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

        protected override void OnDestroy() => _voice.Dispose();

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
                if (cue.Slot == CueSlot.Announce && _visibility.IsSeen(cue))
                {
                    _queue.Offer(set, cue.Announcement, now);
                }
            }

            if (!_voice.IsPlaying(0) && _queue.Count > 0)
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

            cue.Pick(out var clip, out var pitch);
            var source = _voice.Load(0, cue, clip, pitch);
            // Alerts are heard the same wherever the camera is.
            source.spatialBlend = 0f;
            source.Play();
        }
    }
}
