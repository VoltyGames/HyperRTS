using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A vehicle heavy enough to flatten <see cref="Destructible"/> scenery it drives over.</summary>
    public struct Crusher : IComponentData
    {
        /// <summary>Compared with <see cref="Destructible.Crush"/>: a size-3 tank flattens anything rated 1 to 3.</summary>
        public int Size;
    }
}
