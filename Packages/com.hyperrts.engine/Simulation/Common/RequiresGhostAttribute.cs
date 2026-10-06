using System;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// The server spawns or changes this object, so it must be a ghost to reach multiplayer clients. Inherited, so
    /// subclasses (a game's own units) are covered too.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class RequiresGhostAttribute : Attribute
    {
    }
}
