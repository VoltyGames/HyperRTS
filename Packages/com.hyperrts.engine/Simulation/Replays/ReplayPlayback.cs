using Unity.Entities;
using Unity.Mathematics;

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

        /// <summary>Plays forward at <see cref="Speed"/>, keeping a seek inside the recording; pauses at the end.</summary>
        public void Advance(float deltaTime)
        {
            if (Playing)
            {
                Time += deltaTime * Speed;
            }

            Time = math.clamp(Time, 0f, Duration);
            if (Time >= Duration)
            {
                Playing = false;
            }
        }
    }
}
