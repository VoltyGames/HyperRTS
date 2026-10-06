using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Passenger predicates shared by orders, targeting and selection.</summary>
    public static class TransportRules
    {
        public static bool IsInside(in ComponentLookup<Inside> inside, Entity entity) =>
            inside.HasComponent(entity) && inside.IsComponentEnabled(entity);
    }
}
