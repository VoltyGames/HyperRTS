using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Logs the camera or a group to a TSV for some seconds, one row per rendered frame.</summary>
    internal sealed class SampleStep : StagingStep
    {
        /// <summary>The value of <c>what</c> that samples the camera instead of a group.</summary>
        public const string CameraKey = "camera";

        public string what;
        public float seconds;

        public override string Output => $"sample:{what}";

        public override IEnumerable<string> Uses => what == CameraKey ? base.Uses : new[] { what };

        public override void Check()
        {
            Need(what, "what");
            if (seconds <= 0f)
            {
                throw new InvalidOperationException($"needs 'seconds' above 0, not {seconds}");
            }
        }

        public override void Run(StagingRun run) => run.Start(new SampleActivity(run, what, seconds));
    }
}
