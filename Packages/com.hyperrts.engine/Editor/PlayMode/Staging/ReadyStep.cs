using System.Collections.Generic;
using HyperRTS.Editor.PlayMode.Cheats;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Ends a group's ability cooldowns, so a power that charges for minutes fires at once.</summary>
    internal sealed class ReadyStep : StagingStep
    {
        private readonly string _units;

        private ReadyStep(string units) => _units = units;

        public override string Kind => "ready";

        public override IEnumerable<string> Uses => new[] { _units };

        public static StagingStep Read(StagingObject step) => new ReadyStep(step.Text("ready"));

        public override void Run(StagingRun run)
        {
            using var listed = new NativeArray<Entity>(run.LiveUnits(_units, "ready").ToArray(), Allocator.Temp);
            MatchCheats.ReadyAbilities(run.World, listed);
        }
    }
}
