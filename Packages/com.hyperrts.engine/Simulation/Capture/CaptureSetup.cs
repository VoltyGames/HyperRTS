using HyperRTS.Simulation.Common.Authoring;


namespace HyperRTS.Simulation.Capture
{
    /// <summary>Adds the capture components to buildings and units.</summary>
    public static class CaptureSetup
    {
        public static void AddCapturable(EntityWriter writer, float captureTime)
        {
            writer.Add(new Capturable { CaptureTime = captureTime });
            writer.Add<CaptureProgress>();
        }

        public static void AddCapturer(EntityWriter writer, float rate, bool consumedOnCapture) =>
            writer.Add(new Capturer { Rate = rate, ConsumedOnCapture = consumedOnCapture });
    }
}
