using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Hurts every live unit of a group by an amount through the damage system.</summary>
    internal sealed class DamageStep : StagingStep
    {
        private readonly string _units;
        private readonly float _amount;

        private DamageStep(StagingJson json)
        {
            _units = json.Text("units");
            _amount = json.Number("amount");
        }

        public override IEnumerable<string> Uses => new[] { _units };

        public static StagingStep Read(StagingJson step) => new DamageStep(step.Child("damage"));

        public override void Run(StagingRun run)
        {
            using var listed = run.LiveUnitArray(_units, "damage");
            MatchCheats.Damage(run.World, listed, _amount);
        }
    }
}
