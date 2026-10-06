using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// Singleton while a replay plays (see <see cref="ReplayViewer"/>). A HUD reads it and writes <see cref="Speed"/>,
    /// <see cref="Playing"/> or <see cref="Time"/>: writing the time seeks.
    /// </summary>
    public struct ReplayPlayback : IComponentData
    {
        /// <summary>Seconds since the recording started.</summary>
        public float Time;
        public float Duration;

        /// <summary>Playback rate while <see cref="Playing"/>: 1 is real time.</summary>
        public float Speed;
        public bool Playing;
    }
}
