using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Kills a group through the damage system, so death effects play as in a fight.</summary>
    internal sealed class DestroyStep : StagingStep
    {
        private readonly string _units;

        private DestroyStep(string units) => _units = units;

        public override string Kind => "destroy";

        public override IEnumerable<string> Uses => new[] { _units };

        public static StagingStep Read(StagingJson step) => new DestroyStep(step.Text("destroy"));

        public override void Run(StagingRun run)
        {
            using var listed = new NativeArray<Entity>(run.LiveUnits(_units, "destroy").ToArray(), Allocator.Temp);
            MatchCheats.Destroy(run.World, listed);
        }
    }
}
