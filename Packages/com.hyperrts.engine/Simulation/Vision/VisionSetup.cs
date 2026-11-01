using HyperRTS.Simulation.Common.Authoring;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Adds a viewer's sight: its range and eye, and the patch its sight is drawn into.</summary>
    public static class VisionSetup
    {
        public static void AddVision<TWriter>(ref TWriter writer, float range, float eyeHeight)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new VisionRange { Value = range, EyeHeight = eyeHeight });
            writer.Add(new SightPatch { Radius = -1 });
            writer.AddBuffer<SightPatchBits>();
        }
    }
}
