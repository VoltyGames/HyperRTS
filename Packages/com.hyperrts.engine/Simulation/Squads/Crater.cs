using Unity.Entities;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A cover entity <see cref="CraterSystem"/> dug where a heavy blast hit open ground; <see cref="Order"/> counts them, so the oldest goes first.</summary>
    public struct Crater : IComponentData
    {
        public uint Order;
    }

    /// <summary>On the match, when it names a crater prefab: which blasts dig a crater and how many may lie on the map.</summary>
    public struct CraterRules : IComponentData
    {
        /// <summary>Carries the <see cref="CoverSpot"/>s of one crater.</summary>
        public Entity Prefab;

        /// <summary>Smallest splash radius in metres of a blast that digs one.</summary>
        public float MinRadius;

        public int MaxCount;

        /// <summary>Metres a new crater keeps from every other crater and cover spot.</summary>
        public float Spacing;
    }
}
