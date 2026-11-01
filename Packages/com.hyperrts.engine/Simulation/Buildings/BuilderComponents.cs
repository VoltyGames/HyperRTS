using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Stats;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a unit construct allied sites; <see cref="Rate"/> scales its build speed.</summary>
    public struct Builder : IComponentData
    {
        [StatField(Stat.BuildRate)] public float Rate;
    }

    /// <summary>A building prefab a builder can place.</summary>
    [InternalBufferCapacity(4)]
    public struct BuildOption : IBufferElementData, IPrefabOption
    {
        public Entity Prefab;

        /// <summary>On the command card's second page, behind a "More" card.</summary>
        public bool Advanced;

        Entity IPrefabOption.Prefab => Prefab;
    }
}
