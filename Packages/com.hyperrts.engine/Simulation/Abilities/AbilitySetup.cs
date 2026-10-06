using System.Collections.Generic;
using HyperRTS.Simulation.Common;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Adds an entity's abilities in command-card order.</summary>
    public static class AbilitySetup
    {
        public static void Add<TWriter>(ref TWriter writer, IReadOnlyList<Ability> abilities)
            where TWriter : struct, IEntityWriter => writer.AddBuffer(abilities);
    }
}
