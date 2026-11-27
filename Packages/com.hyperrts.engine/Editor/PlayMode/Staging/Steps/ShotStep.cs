using System.IO;
using HyperRTS.Editor.PlayMode.Capture;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>One Game view screenshot, taken through the Recorder so it is a settled, rendered frame.</summary>
internal sealed class ShotStep(string name) : StagingStep
{
    private const int Fps = 30;

    // Two frames: the first can predate this step's changes, so the second is kept.
    private const float Seconds = 1.5f / Fps;

    public override string Output => $"shot:{name}";

    public override void Run(StagingRun run)
    {
        var folder = $"{run.Folder}/.{name}";
        var path = $"{run.Folder}/{name}.png";
        run.Start(new CaptureActivity(new FrameCapture(folder, Seconds, Fps) { Size = run.Resolution }, frames =>
        {
            File.Delete(path);
            File.Move(frames[^1], path);
            Directory.Delete(folder, true);
            run.Report.shots.Add(path);
        }));
    }
}
