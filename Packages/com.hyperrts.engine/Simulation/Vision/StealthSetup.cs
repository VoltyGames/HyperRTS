using HyperRTS.Simulation.Common.Authoring;


namespace HyperRTS.Simulation.Vision
{
    /// <summary>Adds the stealth and detector components.</summary>
    public static class StealthSetup
    {
        public static void AddStealth(EntityWriter writer, in Stealth stealth, bool enabled)
        {
            writer.Add(stealth);
            writer.SetEnabled<Stealth>(enabled);

            // Set now so a fresh spawn is never visible for the frame before StealthSystem runs.
            writer.Add<Stealthed>();
            writer.SetEnabled<Stealthed>(enabled && !stealth.InConcealment);
            writer.Add<Concealed>();
            writer.SetEnabled<Concealed>(false);
        }

        public static void AddDetector(EntityWriter writer, float radius) =>
            writer.Add(new Detector { Radius = radius });
    }
}
