using System;
using HyperRTS.Simulation.Match;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Sets the game speed of a single-player match.</summary>
    internal sealed class SpeedStep : StagingStep
    {
        private readonly float _scale;

        private SpeedStep(StagingObject json)
        {
            _scale = json.Number("speed");
            if (_scale <= 0f)
            {
                throw json.Problem($"needs a 'speed' above 0, not {_scale}");
            }
        }

        public override string Kind => "speed";

        public static StagingStep Read(StagingObject step) => new SpeedStep(step);

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            if (!PlayWorld.TryGetSingleton(entityManager, out GameSpeed speed))
            {
                throw new InvalidOperationException("this match has no game speed; only single player does");
            }

            PlayWorld.SetSingleton(entityManager, speed.WithScale(_scale));
        }
    }
}
