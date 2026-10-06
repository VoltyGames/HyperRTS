using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>The world MonoBehaviours present (single player or this client), while one exists.</summary>
    public static class DefaultWorld
    {
        /// <summary>False in edit mode, while loading and between network sessions.</summary>
        public static bool TryGet(out World world)
        {
            world = World.DefaultGameObjectInjectionWorld;
            return world != null && world.IsCreated;
        }

        public static bool TryGetEntityManager(out EntityManager entityManager)
        {
            entityManager = default;
            if (!TryGet(out var world))
            {
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }
    }
}
