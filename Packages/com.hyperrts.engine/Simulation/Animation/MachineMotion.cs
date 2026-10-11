namespace HyperRTS.Simulation.Animation
{
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
