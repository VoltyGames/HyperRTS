using HyperRTS.Editor.PlayMode.Capture;
using System;
using System.Threading.Tasks;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>A <see cref="FrameCapture"/> in progress; hands its PNG paths to the step when it ends.</summary>
    internal sealed class CaptureActivity : StagingActivity
    {
        private readonly Task<string[]> _recording;
        private readonly Action<string[]> _done;

        public CaptureActivity(FrameCapture capture, Action<string[]> done)
        {
            _recording = capture.Record();
            _done = done;
        }

        public override bool Records => true;

        public override bool Advance(StagingRun run)
        {
            if (!_recording.IsCompleted)
            {
                return false;
            }

            _done(_recording.GetAwaiter().GetResult());
            return true;
        }
    }
}
