using HyperRTS.Simulation.Common.Players;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Production
{
    /// <summary>The parts of one producer the production job reads beside its <see cref="Producer"/>.</summary>
    internal struct ProducerInput
    {
        public Entity Entity;
        public LocalTransform Transform;
        public Owner Owner;
        public RallyPoint Rally;
        public bool HasRally;
    }
}
