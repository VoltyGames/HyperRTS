using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Core
{
    /// <summary>
    /// Builds an entity's component set into a command buffer, so systems and jobs can spawn without a sync point.
    /// Since Entities 6.6 the returned <see cref="Entity"/> is the real one (not a placeholder), valid before and
    /// after playback; it has no components until the buffer plays back.
    /// </summary>
    public interface IEntityFactory
    {
        Entity CreateEntity(EntityCommandBuffer ecb);
    }

    public static class EntityFactoryExtensions
    {
        /// <summary>Creates the entity immediately (records into a temp buffer and plays it back).</summary>
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
