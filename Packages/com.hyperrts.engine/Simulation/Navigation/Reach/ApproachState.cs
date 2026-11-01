namespace HyperRTS.Simulation.Navigation.Reach
{
    /// <summary>Where a walk up to a target stands this frame.</summary>
    public enum ApproachState : byte
    {
        Walking = 0,
        Reached = 1,

        /// <summary>The walk ended by itself short of the target: the way is shut or the target is unreachable.</summary>
        Stuck = 2,
    }
}
