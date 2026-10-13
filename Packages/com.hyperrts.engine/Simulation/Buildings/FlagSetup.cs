using System;
using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Adds the flag mount to buildings.</summary>
    public static class FlagSetup
    {
        /// <summary>A fully lowered flag keeps one flag height of pole between it and the pole foot.</summary>
        public static void AddFlagMount<TWriter>(ref TWriter writer, Entity flag, float flagHeight,
            float poleHeight) where TWriter : struct, IEntityWriter
        {
            if (flagHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(flagHeight), flagHeight, "Flag height must be positive.");
            }

            var drop = poleHeight / flagHeight - 2f;
            if (drop < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(poleHeight), poleHeight,
                    $"A {poleHeight} m pole is too short for a {flagHeight} m flag (at least 2 flag heights).");
            }

            if (drop > FlagMount.MaxDrop)
            {
                throw new ArgumentOutOfRangeException(nameof(poleHeight), poleHeight,
                    $"A {poleHeight} m pole is too tall for a {flagHeight} m flag (at most {FlagMount.MaxDrop + 2f} flag heights).");
            }

            writer.Add(new FlagMount { Flag = flag, Drop = drop });
        }
    }
}
