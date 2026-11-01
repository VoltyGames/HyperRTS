using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Creates the per-frame queue singletons that <see cref="EventWriter{T}"/>s append to.</summary>
    public static class EventQueues
    {
        /// <summary>Call from the owning system's OnCreate; the entity takes the name of its tag.</summary>
        public static void Create<TQueue>(ref SystemState state, params ComponentType[] events)
            where TQueue : unmanaged, IComponentData
        {
            var queue = state.EntityManager.CreateEntity(typeof(TQueue));
            state.EntityManager.AddComponent(queue, new ComponentTypeSet(events));
            state.EntityManager.SetName(queue, typeof(TQueue).Name);
        }
    }
}
