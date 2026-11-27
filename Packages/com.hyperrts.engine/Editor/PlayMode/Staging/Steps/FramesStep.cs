using System;
using HyperRTS.Editor.PlayMode.Capture;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>A burst of Game view frames at a fixed rate, or a ProRes movie with the game's sound, through <see cref="FrameCapture"/>.</summary>
internal sealed class FramesStep : StagingStep
{
    public string name;
    public float seconds;
    public int fps;

    private bool _movie;

    public override string Output => $"{Kind}:{name}";

    /// <summary>Makes this a ProRes movie instead of a burst of frames.</summary>
    public FramesStep AsMovie()
    {
        _movie = true;
        return this;
    }

    public override void Check()
    {
        Need(name, "name");
        if (seconds <= 0f || fps <= 0)
        {
            throw new InvalidOperationException($"needs 'seconds' and 'fps' above 0, got {seconds} and {fps}");
        }
    }

    public override void Run(StagingRun run)
    {
        var folder = $"{run.Folder}/{name}";
        var capture = new FrameCapture(folder, seconds, fps) { Size = run.Resolution, Movie = _movie };
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
