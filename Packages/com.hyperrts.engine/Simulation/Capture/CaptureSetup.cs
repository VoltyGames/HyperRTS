using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Capture
{
    /// <summary>Adds the capture components to buildings and units.</summary>
    public static class CaptureSetup
    {
        public static void AddCapturable<TWriter>(ref TWriter writer, float captureTime)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Capturable { CaptureTime = captureTime });
            writer.Add<CaptureProgress>();
        }

        public static void AddCapturer<TWriter>(ref TWriter writer, float rate, bool consumedOnCapture)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new Capturer { Rate = rate, ConsumedOnCapture = consumedOnCapture });
    }
}
