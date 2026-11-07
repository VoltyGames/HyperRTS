using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Optional client-side singleton that shows listed owners in other colours (colour-blind palettes, own / ally /
    /// enemy colouring). Only presentation reads it; the replicated <c>Player.Color</c> is untouched.
    /// </summary>
    public struct TeamColorOverride : IComponentData
    {
        public FixedList512Bytes<OwnerColor> Colors;

        public bool TryGet(byte owner, out float4 color)
        {
            foreach (var entry in Colors)
            {
                if (entry.Owner == owner)
                {
                    color = entry.Color;
                    return true;
                }
            }

            color = default;
            return false;
        }
    }
}
