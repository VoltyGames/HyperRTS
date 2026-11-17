namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A burst of Game view frames at a fixed rate, through <see cref="FrameCapture"/>.</summary>
    internal sealed class FramesStep : StagingStep
    {
        private readonly string _name;
        private readonly float _seconds;
        private readonly int _fps;

        private FramesStep(StagingObject json)
        {
            _name = json.Text("name");
            _seconds = json.Number("seconds");
            _fps = json.Whole("fps");
            if (_seconds <= 0f || _fps <= 0)
            {
                throw json.Problem($"needs 'seconds' and 'fps' above 0, got {_seconds} and {_fps}");
            }
        }

        public override string Kind => "frames";

        public override string Output => $"frames:{_name}";

        public static StagingStep Read(StagingObject step) => new FramesStep(step.Child("frames"));

        public override void Run(StagingRun run)
        {
            var folder = $"{run.Folder}/{_name}";
            run.Start(new CaptureActivity(new FrameCapture(folder, _seconds, _fps), _ => run.Report.frames.Add(folder)));
        }
    }
}
