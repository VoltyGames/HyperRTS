using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The one way to give an entity a new owner, so every module can react to <see cref="OwnerChanged"/>.</summary>
    public static class Ownership
    {
        public static void Transfer(EntityCommandBuffer ecb, DynamicBuffer<OwnerChanged> events, Entity entity,
            byte from, byte to)
        {
            ecb.SetComponent(entity, new Faction { Value = to });
            events.Add(new OwnerChanged { Entity = entity, From = from, To = to });
        }
    }
}
