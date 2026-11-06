namespace HyperRTS.Simulation.Squads
{
    /// <summary>Why a squad can't start a Retreat or Reinforce, from the furthest from able to the nearest.</summary>
    public enum SquadRefusal : byte
    {
        None,

        /// <summary>No member is dead.</summary>
        FullStrength,

        /// <summary>No friendly working point to go to, or none in range.</summary>
        NoPoint,

        /// <summary>The player can't pay for the next member.</summary>
        TooPoor,
    }
}
