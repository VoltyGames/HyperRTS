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

        [Tooltip("Variations; one is picked at random each time the cue plays, never the same one twice in a row.")]
        public AudioClip[] clips = { };

        [Tooltip("Random volume between x and y.")]
        public Vector2 volume = new(0.9f, 1f);

        [Tooltip("Random pitch between x and y.")]
        public Vector2 pitch = new(0.95f, 1.05f);

        [Tooltip("Most copies of this cue playing at once; further plays are skipped.")]
        [Min(1)]
        public int maxInstances = 4;

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

        /// <summary>Draws one of the clips, never the one drawn last, and a pitch, for a cue that has clips.</summary>
        public void Pick(out AudioClip clip, out float randomPitch)
        {
            // A step of 1 to n - 1 clips on from the last lands on every other clip alike.
            _last = _last >= 0 && clips.Length > 1
                ? (_last + Random.Range(1, clips.Length)) % clips.Length
                : Random.Range(0, clips.Length);
            clip = clips[_last];
            randomPitch = Random.Range(pitch.x, pitch.y);
        }

        public float PickVolume() => Random.Range(volume.x, volume.y);
    }
}
