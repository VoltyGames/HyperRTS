using System;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds <see cref="Decay"/> to remains.</summary>
    public static class DecaySetup
    {
        public static void Add<TWriter>(ref TWriter writer, string owner, float lifetime, float sinkDuration,
            float sinkDepth) where TWriter : struct, IEntityWriter
        {
            Require(lifetime > 0f, owner, "lifetime");
            Require(sinkDuration > 0f, owner, "sinkDuration");
            Require(sinkDepth > 0f, owner, "sinkDepth");
            writer.Add(new Decay { Lifetime = lifetime, SinkDuration = sinkDuration, SinkDepth = sinkDepth });
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
