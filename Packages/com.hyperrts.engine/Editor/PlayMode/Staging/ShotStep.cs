using System.IO;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>One Game view screenshot, taken through the Recorder so it is a settled, rendered frame.</summary>
    internal sealed class ShotStep : StagingStep
    {
        private const int Fps = 30;

        // Two frames: the first can predate this step's changes, so the second is kept.
        private const float Seconds = 1.5f / Fps;

        private readonly string _name;

        private ShotStep(string name) => _name = name;

        public override string Kind => "shot";

        public override string Output => $"shot:{_name}";

        public static StagingStep Read(StagingObject step) => new ShotStep(step.Text("shot"));

        public override void Run(StagingRun run)
        {
            var folder = $"{run.Folder}/.{_name}";
            var path = $"{run.Folder}/{_name}.png";
            run.Start(new CaptureActivity(new FrameCapture(folder, Seconds, Fps) { Size = run.Resolution }, frames =>
            {
                File.Delete(path);
                File.Move(frames[^1], path);
                Directory.Delete(folder, true);
                run.Report.shots.Add(path);
            }));
        }
    }
}
