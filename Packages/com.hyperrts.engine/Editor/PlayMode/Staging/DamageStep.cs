using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Hurts every live unit of a group by an amount through the damage system.</summary>
    internal sealed class DamageStep : StagingStep
    {
        private readonly string _units;
        private readonly float _amount;

        private DamageStep(StagingObject json)
        {
            _units = json.Text("units");
            _amount = json.Number("amount");
        }

        public override string Kind => "damage";

        public override IEnumerable<string> Uses => new[] { _units };

        public static StagingStep Read(StagingObject step) => new DamageStep(step.Child("damage"));

        public override void Run(StagingRun run)
        {
            using var listed = new NativeArray<Entity>(run.LiveUnits(_units, "damage").ToArray(), Allocator.Temp);
            MatchCheats.Damage(run.World, listed, _amount);
        }
    }
}
