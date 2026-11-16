using HyperRTS.Simulation.Common.Authoring;
using System;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace HyperRTS.Simulation.Cues.Sounds
{
    /// <summary>
    /// A playable sound: random clip, never the one it played last, random volume and pitch, with an instance limit and
    /// a priority for mixing.
    /// </summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Audio + "Sound Cue", fileName = "SoundCue")]
    [HelpURL(HyperRTSDocs.Audio)]
    public class SoundCue : ScriptableObject
    {
        [NonSerialized]
        private int _last = -1;

        [NonSerialized]
        private int _lastDistant = -1;

        [Tooltip("Variations; one is picked at random each time the cue plays, never the same one twice in a row.")]
        public AudioClip[] clips = { };

        [Tooltip("Random volume between x and y.")]
        public Vector2 volume = new(0.9f, 1f);

        [Tooltip("Random pitch between x and y.")]
        public Vector2 pitch = new(0.95f, 1.05f);

        [Tooltip("Most copies of this cue playing at once; further plays are skipped.")]
        [Min(1)]
        public int maxInstances = 4;

        [Tooltip("Seconds after a play before the cue may play again; further plays are skipped. 0 never waits.")]
        [Min(0f)]
        public float cooldown;

        [Tooltip("0 is most important. When every voice is busy, a cue replaces a less important one.")]
        [Range(0, 256)]
        public int priority = 128;

        [Tooltip("Optional mixer group (weapons, voice, UI) that sets the category balance.")]
        public AudioMixerGroup mixerGroup;

        [Tooltip("Plays in 3D at the event position; off plays 2D (voice, UI, alerts).")]
        public bool spatial = true;

        [Tooltip("3D only: full volume within this distance of the listener.")]
        [Min(0f)]
        public float minDistance = 10f;

        [Tooltip("3D only: silent (and skipped) beyond this distance from the listener.")]
        [Min(0.1f)]
        public float maxDistance = 60f;

        [Tooltip("3D only: how wide the sound is, in degrees. 0 pans fully to one side; 180 never pans.")]
        [Range(0f, 360f)]
        public float spread = 60f;

        [Tooltip("3D only: variations heard from beyond Distant From, such as a far boom without the near crack. " +
                 "Empty plays the clips at every distance.")]
        public AudioClip[] distantClips = { };

        [Tooltip("3D only: from this distance to the listener the distant variations play instead of the clips.")]
        [Min(0f)]
        public float distantFrom = 40f;

        /// <summary>Whether a play this far from the listener draws from the distant variations.</summary>
        public bool IsDistant(float distance) => spatial && distantClips.Length > 0 && distance >= distantFrom;

        /// <summary>
        /// Draws a clip from the near or distant set, never the one that set drew last, and a pitch, for a cue whose
        /// set has clips.
        /// </summary>
        public void Pick(bool distant, out AudioClip clip, out float randomPitch)
        {
            clip = distant
                ? distantClips[Draw(distantClips.Length, ref _lastDistant)]
                : clips[Draw(clips.Length, ref _last)];
            randomPitch = Random.Range(pitch.x, pitch.y);
        }

        public float PickVolume() => Random.Range(volume.x, volume.y);

        private static int Draw(int count, ref int last)
        {
            if (last < 0 || count == 1)
            {
                last = Random.Range(0, count);
                return last;
            }

            // Draw among the other clips: indexes from the last one up shift by one to skip it.
            var next = Random.Range(0, count - 1);
            last = next >= last ? next + 1 : next;
            return last;
        }
    }
}
