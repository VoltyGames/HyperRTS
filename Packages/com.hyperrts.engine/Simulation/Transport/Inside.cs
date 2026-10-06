using HyperRTS.Simulation.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Enabled while the passenger is inside <see cref="Container"/>: hidden, untargetable, carried along, and firing
    /// out only if the container allows it.
    /// </summary>
    public struct Inside : IComponentData, IEnableableComponent
    {
        public Entity Container;

        /// <summary>Stance restored on exit; inside, passengers hold position or stay passive.</summary>
        public Stance Stance;
    }
}
