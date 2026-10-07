using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// Optional client-side singleton that shows listed factions in other colours (colour-blind palettes, own / ally /
    /// enemy colouring). Only presentation reads it; the replicated <c>Player.Color</c> is untouched.
    /// </summary>
    public struct TeamColorOverride : IComponentData
    {
        public FixedList512Bytes<FactionColor> Colors;

        public bool TryGet(byte faction, out float4 color)
        {
            foreach (var entry in Colors)
            {
                if (entry.Faction == faction)
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
