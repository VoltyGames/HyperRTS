using System;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>Who an area aura affects: pick at least one relation and one kind.</summary>
    [Flags]
    public enum AuraTargets : byte
    {
        None = 0,
        Own = 1 << 0,
        Allies = 1 << 1,
        Enemies = 1 << 2,
        Neutral = 1 << 3,
        Units = 1 << 4,
        Buildings = 1 << 5,

        Friendly = Own | Allies,
        Everyone = Own | Allies | Enemies | Neutral,
        AllKinds = Units | Buildings,
    }
}
