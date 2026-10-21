using System;
using HyperRTS.Simulation.Cues;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// A fixed set of audio sources under one DontSave root, made on first use. The root outlives scene reloads, so
    /// its owner disposes it with the world.
    /// </summary>
    public sealed class VoiceBank : IDisposable
    {
        private readonly string _name;
        private readonly int _count;
        private readonly bool _loop;
        private GameObject _root;
        private AudioSource[] _sources;

        public VoiceBank(string name, int count, bool loop = false)
        {
            _name = name;
            _count = count;
            _loop = loop;
        }

        public AudioSource this[int voice] => Sources()[voice];

        /// <summary>False before the first voice is made, without making it.</summary>
        public bool IsPlaying(int voice) => _root != null && _sources[voice].isPlaying;

        /// <summary>Stops the voice and sets it up for the cue with the clip and pitch the caller drew.</summary>
        public AudioSource Load(int voice, SoundCue cue, AudioClip clip, float pitch)
        {
            var source = this[voice];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Random.Range(cue.volume.x, cue.volume.y);
            source.priority = cue.priority;
            source.outputAudioMixerGroup = cue.mixerGroup;
            source.spatialBlend = cue.spatial ? 1f : 0f;
            source.minDistance = cue.minDistance;
            source.maxDistance = cue.maxDistance;
            return source;
        }

        public void Dispose() => CoreUtils.Destroy(_root);

        private AudioSource[] Sources()
        {
            if (_root != null)
            {
                return _sources;
            }

            _root = new GameObject(_name) { hideFlags = HideFlags.DontSave };
            _sources = new AudioSource[_count];
            for (var i = 0; i < _sources.Length; i++)
            {
                var voice = new GameObject($"Voice {i}") { hideFlags = HideFlags.DontSave };
                voice.transform.SetParent(_root.transform);
                _sources[i] = voice.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].loop = _loop;
                _sources[i].rolloffMode = AudioRolloffMode.Linear;
            }

            return _sources;
        }
    }
}
