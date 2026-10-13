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
            // The builder, not GetEntityQuery's params array: Burst-compiled OnCreates call this.
            _players = new EntityQueryBuilder(Allocator.Temp).WithAll<Player>().Build(ref state),
            _override = new EntityQueryBuilder(Allocator.Temp).WithAll<TeamColorOverride>().Build(ref state),
        };

        public NativeArray<float4> Build(AllocatorManager.AllocatorHandle allocator)
        {
            var colors = CollectionHelper.CreateNativeArray<float4>(Size, allocator);
            var hasOverride = _override.TryGetSingleton(out TeamColorOverride colorOverride);
            var players = _players.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                var color = player.Color;
                if (hasOverride && colorOverride.TryGet(player.Faction, out var overridden))
                {
                    color = overridden;
                }

                colors[player.Faction] = new float4(color.xyz, 1f);
            }

            return colors;
        }
    }
}
