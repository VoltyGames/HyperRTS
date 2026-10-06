using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Who last damaged this entity; read on death for kill credit.</summary>
    public struct LastAttacker : IComponentData
    {
        public Entity Source;
        public byte Faction;
    }
}
