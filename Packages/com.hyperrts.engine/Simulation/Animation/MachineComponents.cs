using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>Running time of a machine (pumpjack, turbine) that its <see cref="MachinePart"/>s move by.</summary>
    public struct MachineClock : IComponentData
    {
        public float Time;

        /// <summary>Stands still while neutral: a captured derrick starts pumping.</summary>
        public bool OwnedOnly;
    }

    /// <summary>A moving piece of a machine, posed every frame from its machine's clock.</summary>
    public struct MachinePart : IComponentData
    {
        public Entity Machine;
        public MachineMotion Motion;

        /// <summary>Local rotation axis (Spin, Swing).</summary>
        public float3 Axis;

        /// <summary>Degrees per second of the cycle; a swing completes one rock per 360°.</summary>
        public float Speed;

        /// <summary>Swing: largest angle either way of <see cref="Offset"/>, degrees.</summary>
        public float Amplitude;

        /// <summary>Swing: the angle (degrees from rest) it rocks about; a beam at rest at one end of its stroke needs one.</summary>
        public float Offset;

        /// <summary>Degrees added to the cycle, to set parts relative to each other.</summary>
        public float Phase;

        /// <summary>Link: the point it sits on and the point it aims at.</summary>
        public Entity Follow;
        public Entity Aim;

        /// <summary>The baked local rotation the motion starts from.</summary>
        public quaternion Rest;
    }

    /// <summary>How a <see cref="MachinePart"/> moves.</summary>
    public enum MachineMotion : byte
    {
        /// <summary>Turns steadily about its axis (a crank, a rotor).</summary>
        Spin = 0,

        /// <summary>Rocks to and fro about its axis in step with the spin (a walking beam).</summary>
        Swing = 1,

        /// <summary>Sits on one point and aims its up axis at another (a connecting rod).</summary>
        Link = 2,
    }
}
