using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Adds the capture components to buildings and units.</summary>
    public static class CaptureSetup
    {
        public static void AddCapturable<TSink>(ref TSink sink, float captureTime) where TSink : struct, IComponentSink
        {
            sink.Add(new Capturable { CaptureTime = captureTime });
            sink.Add<CaptureProgress>();
        }

        public static void AddCapturer<TSink>(ref TSink sink, float rate, bool consumedOnCapture)
            where TSink : struct, IComponentSink =>
            sink.Add(new Capturer { Rate = rate, ConsumedOnCapture = consumedOnCapture });
    }
}
