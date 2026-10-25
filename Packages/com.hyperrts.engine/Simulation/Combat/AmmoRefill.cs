namespace HyperRTS.Simulation.Combat
{
    /// <summary>Where and how a limited weapon gets its rounds back.</summary>
    public enum AmmoRefill : byte
    {
        /// <summary>One round per reload time while docked at an airfield.</summary>
        Docked = 0,

        /// <summary>One round per reload time, anywhere.</summary>
        Round = 1,

        /// <summary>Every round at once, one reload time after the magazine runs empty, anywhere.</summary>
        Magazine = 2,
    }
}
