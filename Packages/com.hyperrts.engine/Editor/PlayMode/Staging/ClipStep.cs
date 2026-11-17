namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A ProRes movie of the Game view with the game's sound, through <see cref="FrameCapture"/>.</summary>
    internal sealed class ClipStep : StagingStep
    {
        private readonly string _name;
        private readonly float _seconds;
        private readonly int _fps;

        private ClipStep(StagingObject json)
        {
            _name = json.Text("name");
            _seconds = json.Number("seconds");
            _fps = json.Whole("fps");
            if (_seconds <= 0f || _fps <= 0)
            {
                throw json.Problem($"needs 'seconds' and 'fps' above 0, got {_seconds} and {_fps}");
            }
        }

        public override string Kind => "clip";

        public override string Output => $"clip:{_name}";

        public static StagingStep Read(StagingObject step) => new ClipStep(step.Child("clip"));

        public override void Run(StagingRun run)
        {
            var capture = new FrameCapture($"{run.Folder}/{_name}", _seconds, _fps) { Size = run.Resolution, Movie = true };
            run.Start(new CaptureActivity(capture, movie => run.Report.clips.Add(movie[0])));
        }
    }
}
