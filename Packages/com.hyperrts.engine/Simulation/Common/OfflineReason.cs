using System;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Why an entity is <see cref="Offline"/>. Several can hold at once.</summary>
    [Flags]
    public enum OfflineReason : uint
    {
        None = 0,

        /// <summary>Recomputed every frame by <c>OfflineSystem</c> from <see cref="ConstructionProgress"/>.</summary>
        UnderConstruction = 1 << 0,

        /// <summary>Set every frame by <c>PowerSystem</c> on power consumers while their owner's power is low.</summary>
        Unpowered = 1 << 1,

        /// <summary>The engine's own reasons.</summary>
        Engine = UnderConstruction | Unpowered,

        /// <summary>Reasons from here up belong to games (EMP, stun, hacking); the engine never sets them.</summary>
        Custom = 1 << 8,
    }
}
