namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>A burst of Game view frames at a fixed rate, or a ProRes movie with the game's sound, through <see cref="FrameCapture"/>.</summary>
    internal sealed class FramesStep : StagingStep
    {
        private readonly string _name;
        private readonly float _seconds;
        private readonly int _fps;
        private readonly bool _movie;

        private FramesStep(StagingObject json, bool movie)
        {
            _name = json.Text("name");
            _seconds = json.Number("seconds");
            _fps = json.Whole("fps");
            _movie = movie;
            if (_seconds <= 0f || _fps <= 0)
            {
                throw json.Problem($"needs 'seconds' and 'fps' above 0, got {_seconds} and {_fps}");
            }
        }

        public override string Kind => _movie ? "clip" : "frames";

        public override string Output => $"{Kind}:{_name}";

        public static StagingStep Read(StagingObject step) => new FramesStep(step.Child("frames"), false);

        public static StagingStep ReadClip(StagingObject step) => new FramesStep(step.Child("clip"), true);

        public override void Run(StagingRun run)
        {
            var folder = $"{run.Folder}/{_name}";
            var capture = new FrameCapture(folder, _seconds, _fps) { Size = run.Resolution, Movie = _movie };
            run.Start(new CaptureActivity(capture, files =>
            {
                if (_movie)
                {
                    run.Report.clips.Add(files[0]);
                }
                else
                {
                    run.Report.frames.Add(folder);
                }
            }));
        }
    }
}
