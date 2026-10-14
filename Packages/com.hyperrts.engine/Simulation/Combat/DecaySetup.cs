using System;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds <see cref="Decay"/> to remains.</summary>
    public static class DecaySetup
    {
        public static void Add<TWriter>(ref TWriter writer, string owner, float lifetime, float despawnDuration,
            float despawnDepth) where TWriter : struct, IEntityWriter
        {
            Require(lifetime > 0f, owner, "lifetime");
            Require(despawnDuration > 0f, owner, "despawnDuration");
            Require(despawnDepth > 0f, owner, "despawnDepth");
            writer.Add(new Decay { Lifetime = lifetime, DespawnDuration = despawnDuration, DespawnDepth = despawnDepth });
        }

        private static void Require(bool positive, string owner, string field)
        {
            if (!positive)
            {
                throw new ArgumentException($"Decay on '{owner}': {field} must be above 0.");
            }
        }
    }
}
