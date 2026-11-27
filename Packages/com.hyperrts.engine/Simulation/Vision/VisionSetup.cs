using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Entities;

namespace HyperRTS.Simulation.Vision;

/// <summary>Adds a viewer's sight: its range and eye, and the area its sight is drawn into.</summary>
public static class VisionSetup
{
    public static void AddVision(EntityWriter writer, float range, float eyeHeight)
    {
        writer.Add(new ComponentTypeSet(ComponentType.ReadWrite<VisionRange>(),
            ComponentType.ReadWrite<SightArea>(), ComponentType.ReadWrite<SightAreaBits>()));
        writer.Set(new VisionRange { Value = range, EyeHeight = eyeHeight });
        writer.Set(new SightArea { Radius = -1 });
    }
}
