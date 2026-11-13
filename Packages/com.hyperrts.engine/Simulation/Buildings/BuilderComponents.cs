using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Stats;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a unit construct allied sites; <see cref="Rate"/> scales its build speed.</summary>
    public struct Builder : IComponentData
    {
        [StatField(Stat.BuildRate)] public float Rate;
    }

    /// <summary>Enabled for each frame a builder adds progress to a site, so client machines can show the work.</summary>
    [GhostEnabledBit]
    public struct BuildWork : IComponentData, IEnableableComponent { }

    /// <summary>A building prefab a builder can place.</summary>
    [InternalBufferCapacity(4)]
    public struct BuildOption : IBufferElementData, IPrefabOption
    {
        public Entity Prefab;

        Entity IPrefabOption.Prefab => Prefab;
    }
}
