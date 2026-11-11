using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Game speed and pause of a single-player match, a singleton of the local world that <see cref="GameSpeedSystem"/>
    /// applies to the clock. Networked worlds have none: they run on the server's tick.
    /// </summary>
    public struct GameSpeed : IComponentData
    {
        /// <summary>Speed while not paused.</summary>
        public float Scale;

        public bool Paused;

        public static GameSpeed Normal => new() { Scale = 1f };

        public readonly GameSpeed WithScale(float scale)
        {
            var speed = this;
            speed.Scale = scale;
            return speed;
        }

        public readonly GameSpeed WithPaused(bool paused)
        {
            var speed = this;
            speed.Paused = paused;
            return speed;
        }
    }
}
