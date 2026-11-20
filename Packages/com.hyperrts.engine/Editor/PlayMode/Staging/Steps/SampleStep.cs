using System.Collections.Generic;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>Logs the camera or a group to a TSV for some seconds, one row per rendered frame.</summary>
    internal sealed class SampleStep : StagingStep
    {
        /// <summary>The value of <c>what</c> that samples the camera instead of a group.</summary>
        public const string CameraKey = "camera";

        private readonly string _what;
        private readonly float _seconds;

        private SampleStep(StagingJson json)
        {
            _what = json.Text("what");
            _seconds = json.Number("seconds");
            if (_seconds <= 0f)
            {
                throw json.Problem($"needs 'seconds' above 0, not {_seconds}");
            }
        }

        public override string Output => $"sample:{_what}";

        public override IEnumerable<string> Uses => _what == CameraKey ? base.Uses : new[] { _what };

        public static StagingStep Read(StagingJson step) => new SampleStep(step.Child("sample"));

        public override void Run(StagingRun run) => run.Start(new SampleActivity(run, _what, _seconds));
    }
}
