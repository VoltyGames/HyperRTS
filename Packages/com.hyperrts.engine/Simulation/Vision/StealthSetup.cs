using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Adds the stealth and detector components.</summary>
    public static class StealthSetup
    {
        public static void AddStealth<TWriter>(ref TWriter writer, in Stealth stealth, bool enabled)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(stealth);
            writer.SetEnabled<Stealth>(enabled);

            // Set now so a fresh spawn is never visible for the frame before StealthSystem runs.
            writer.Add<Stealthed>();
            writer.SetEnabled<Stealthed>(enabled);
        }

        public static void AddDetector<TWriter>(ref TWriter writer, float radius)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new Detector { Radius = radius });
    }
}
