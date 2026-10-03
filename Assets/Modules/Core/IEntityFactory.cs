using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Core
{
    /// <summary>Records an entity into a command buffer. The returned entity is real at record time.</summary>
    public interface IEntityFactory
    {
        Entity CreateEntity(EntityCommandBuffer ecb);
    }

    public static class EntityFactoryExtensions
    {
        /// <summary>Creates the entity immediately.</summary>
        public static Entity CreateEntity(this IEntityFactory factory, EntityManager entityManager)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = factory.CreateEntity(ecb);
            ecb.Playback(entityManager);
            ecb.Dispose();
            return entity;
        }
    }
}
