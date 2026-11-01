using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Narrows the entities a command addresses, for <see cref="CommandReader.Subjects{TFilter}"/>.</summary>
    public interface ISubjectFilter
    {
        bool Keeps(Entity subject);
    }
}
