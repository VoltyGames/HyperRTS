using HyperRTS.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>A playable sound: random clip, volume and pitch, with an instance limit and a priority for mixing.</summary>
    [CreateAssetMenu(menuName = HyperRTSMenu.Audio + "Sound Cue", fileName = "SoundCue")]
    [HelpURL(HyperRTSDocs.Modules)]
    public class SoundCue : ScriptableObject
    {
        [Tooltip("Variations; one is picked at random each time the cue plays.")]
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
    }
}
