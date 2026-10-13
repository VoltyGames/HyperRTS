using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Remains of a <see cref="Topple"/> piece that presentation shows falling. Their transform already holds the final pose.</summary>
    public struct Felling : IComponentData
    {
        /// <summary>Turns the final crown direction up to where the trunk stood.</summary>
        public quaternion Tilt;

        /// <summary>Unwitnessed falls show lying down at once.</summary>
        public bool Witnessed;
    }
}
