using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Ends a group's ability cooldowns, so a power that charges for minutes fires at once.</summary>
    internal sealed class ReadyStep : StagingStep
    {
        private readonly string _units;

        public ReadyStep(string units) => _units = units;

        public override IEnumerable<string> Uses => new[] { _units };

        public override void Run(StagingRun run)
        {
            using var listed = run.LiveUnitArray(_units, "ready");
            MatchCheats.ReadyAbilities(run.World, listed);
        }
    }
}
