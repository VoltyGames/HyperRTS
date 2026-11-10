using Unity.Entities;

namespace HyperRTS.Simulation.Movement.Crushing
{
    /// <summary>A vehicle heavy enough to flatten <see cref="Destructible"/> scenery and crush enemy <see cref="Crushable"/> units it drives over.</summary>
    public struct Crusher : IComponentData
    {
        /// <summary>Metres per second it must pass to kill the units it drives over.</summary>
        public const float Pace = 1f;

        /// <summary>Compared with <see cref="Destructible.Crush"/> and <see cref="Crushable.Crush"/>: a size-3 tank crushes anything rated 1 to 3.</summary>
        public int Size;
    }
}
