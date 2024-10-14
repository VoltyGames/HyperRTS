using Unity.Entities;

namespace HyperRTS.Core
{
    public interface IEntityFactory
    {
        Entity CreateEntity(EntityManager entityManager);
    }
}
