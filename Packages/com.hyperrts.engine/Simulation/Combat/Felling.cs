using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// On the remains of a <see cref="Topple"/> piece while presentation shows it falling; the simulation already
    /// holds the remains in their final pose.
    /// </summary>
    public struct Felling : IComponentData, IEnableableComponent
    {
        /// <summary>The remains' final rotation.</summary>
        public quaternion Final;

        /// <summary>Turns the final pose's crown direction up to where the trunk stood.</summary>
        public quaternion Tilt;

        /// <summary>Someone saw it fall; otherwise it shows lying down at once.</summary>
        public bool Witnessed;

        /// <summary>Seconds into the fall, kept by presentation.</summary>
        public float Time;
    }
}
