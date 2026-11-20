using System;
using HyperRTS.Simulation.Match;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Sets the game speed of a single-player match.</summary>
    internal sealed class SpeedStep : StagingStep
    {
        private readonly float _scale;

        public SpeedStep(float scale) => _scale = scale;

        public override void Check()
        {
            if (_scale <= 0f)
            {
                throw new InvalidOperationException($"needs a 'speed' above 0, not {_scale}");
            }
        }

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
