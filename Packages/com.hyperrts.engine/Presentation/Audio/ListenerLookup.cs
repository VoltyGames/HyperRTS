using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// The scene's <see cref="AudioListener"/>, searched for at most once a second while there is none, since a
    /// scene-wide search every frame is costly.
    /// </summary>
    public sealed class ListenerLookup
    {
        private const float RetrySeconds = 1f;

        private AudioListener _listener;
        private float _nextSearch = float.NegativeInfinity;

        /// <summary>The listener, or null when the scene has none yet.</summary>
        public AudioListener Find()
        {
            if (_listener != null || Time.unscaledTime < _nextSearch)
            {
                return _listener;
            }

            _nextSearch = Time.unscaledTime + RetrySeconds;
            _listener = Object.FindAnyObjectByType<AudioListener>();
            return _listener;
        }
    }
}
