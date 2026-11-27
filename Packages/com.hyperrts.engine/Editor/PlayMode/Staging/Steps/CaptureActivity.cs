using System;
using System.Threading.Tasks;
using HyperRTS.Editor.PlayMode.Capture;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>A <see cref="FrameCapture"/> in progress; hands its PNG paths to the step when it ends.</summary>
internal sealed class CaptureActivity(FrameCapture capture, Action<string[]> done) : StagingActivity
{
    private readonly FrameCapture _capture = capture;
    private readonly Task<string[]> _recording = capture.Record();

    public override bool Records => true;

    public override bool Advance(StagingRun run)
    {
        if (!_recording.IsCompleted)
        {
            return false;
        }

        done(_recording.GetAwaiter().GetResult());
        return true;
    }

    public override void Stop() => _capture.Stop();
}
