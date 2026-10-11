using System;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Why an entity is <see cref="Offline"/>. Several can hold at once.</summary>
    [Flags]
    public enum OfflineReason : uint
    {
        None = 0,
        UnderConstruction = 1 << 0,
        Unpowered = 1 << 1,

        /// <summary>The engine's own reasons, which <c>OfflineSystem</c> recomputes every frame.</summary>
        Engine = UnderConstruction | Unpowered,

        /// <summary>Reasons from here up belong to games (EMP, stun, hacking); the engine never sets them.</summary>
        Custom = 1 << 8,
    }
}
