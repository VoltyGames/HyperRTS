using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// The colour this client shows each faction in, indexed by faction: its player's, or the
    /// <see cref="TeamColorOverride"/>'s; <c>w == 0</c> marks a faction without a player.
    /// </summary>
    public struct FactionPalette
    {
        public const int Size = byte.MaxValue + 1;

        private EntityQuery _players;
        private EntityQuery _override;

        public static FactionPalette Create(ref SystemState state) => new()
        {
            _players = state.GetEntityQuery(ComponentType.ReadOnly<Player>()),
            _override = state.GetEntityQuery(ComponentType.ReadOnly<TeamColorOverride>()),
        };

        public NativeArray<float4> Build(AllocatorManager.AllocatorHandle allocator)
        {
            var colors = CollectionHelper.CreateNativeArray<float4>(Size, allocator);
            var players = _players.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                colors[player.Faction] = new float4(player.Color.xyz, 1f);
            }

            if (_override.TryGetSingleton(out TeamColorOverride colorOverride))
            {
                ApplyOverride(colors, colorOverride);
            }

            return colors;
        }

        /// <summary>Swaps in the override's colour for every listed faction that has a player.</summary>
        private static void ApplyOverride(NativeArray<float4> colors, in TeamColorOverride colorOverride)
        {
            foreach (var entry in colorOverride.Colors)
            {
                if (colors[entry.Faction].w != 0f)
                {
                    colors[entry.Faction] = new float4(entry.Color.xyz, 1f);
                }
            }
        }
    }
}
