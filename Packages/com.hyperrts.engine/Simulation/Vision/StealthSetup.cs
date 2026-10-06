using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Adds the stealth and detector components.</summary>
    public static class StealthSetup
    {
        public static void AddStealth<TSink>(ref TSink sink, in Stealth stealth, bool enabled)
            where TSink : struct, IComponentSink
        {
            sink.Add(stealth);
            sink.SetEnabled<Stealth>(enabled);

            // Set now so a fresh spawn is never visible for the frame before StealthSystem runs.
            sink.Add<Stealthed>();
            sink.SetEnabled<Stealthed>(enabled);
        }

        public static void AddDetector<TSink>(ref TSink sink, float radius) where TSink : struct, IComponentSink =>
            sink.Add(new Detector { Radius = radius });
    }
}
