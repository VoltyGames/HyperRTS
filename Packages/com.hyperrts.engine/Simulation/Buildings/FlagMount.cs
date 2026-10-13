using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>A building that flies its owner's flag on a pole; presentation hoists and lowers it.</summary>
    public struct FlagMount : IComponentData
    {
        /// <summary>Furthest a flag can be lowered, in flag heights; flag meshes are bounded for it.</summary>
        public const float MaxDrop = 8f;

        /// <summary>The flag's render entity: its origin is the top of the hoist at the pole top, one unit is one flag height.</summary>
        public Entity Flag;

        /// <summary>How far the flag slides down the pole when fully lowered, in flag heights.</summary>
        public float Drop;
    }
}
