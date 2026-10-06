using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Adds an entity's abilities in command-card order.</summary>
    public static class AbilitySetup
    {
        public static void Add<TSink>(ref TSink sink, IReadOnlyList<Ability> abilities)
            where TSink : struct, IComponentSink
        {
            var buffer = sink.AddBuffer<Ability>();
            foreach (var ability in abilities)
            {
                buffer.Add(ability);
            }
        }
    }
}
