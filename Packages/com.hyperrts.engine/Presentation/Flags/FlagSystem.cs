using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>
    /// Shows each <see cref="FlagMount"/>'s flag: whose (the shown faction's <see cref="PlayerSide"/> picks the flag),
    /// how high, its team band, and nothing where the building is fogged for the local player or still being built.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct FlagSystem : ISystem
    {
        private FactionPalette _palette;
        private EntityQuery _sides;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _palette = FactionPalette.Create(ref state);
            _sides = SystemAPI.QueryBuilder().WithAll<Player, PlayerSide>().Build();
            state.RequireForUpdate<FlagMount>();
        }

        // A main-thread loop, not a job: a job may not hold the fog grid's arrays while a world has no grid.
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.TryGetSingleton(out LocalFogView view);
            var hasFog = SystemAPI.TryGetSingleton(out FogOfWar fog);
            SystemAPI.TryGetSingleton(out FactionRelations relations);
            var layers = Layers(state.WorldUpdateAllocator);
            var colors = _palette.Build(state.WorldUpdateAllocator);
            var progress = SystemAPI.GetComponentLookup<CaptureProgress>(true);
            var building = SystemAPI.GetComponentLookup<ConstructionProgress>(true);
            var states = SystemAPI.GetComponentLookup<FlagState>();
            var bands = SystemAPI.GetComponentLookup<FlagColor>();
            state.CompleteDependency();
            foreach (var (mount, faction, place, entity) in SystemAPI
                         .Query<RefRO<FlagMount>, RefRO<Faction>, RefRO<LocalToWorld>>().WithEntityAccess())
            {
                var flag = mount.ValueRO.Flag;
                progress.TryGetComponent(entity, out var capture);
                var owner = faction.ValueRO.Value;
                var shown = FlagDisplay.Resolve(owner, capture, out var hoist);
                var position = place.ValueRO.Position;
                var fogged = hasFog && BuildingFog.IsHidden(in view, in fog, in relations, owner, position);
                var unfinished = building.HasEnabled(entity);
                var visible = shown != Faction.Neutral && !fogged && !unfinished;
                var next = new float4(layers[shown], hoist, visible ? 1f : 0f, mount.ValueRO.Drop);
                if (math.any(states[flag].Value != next))
                {
                    states[flag] = new FlagState { Value = next };
                }

                // Colors' w is 0 for a faction without a player, so its band stays off.
                if (math.any(bands[flag].Value != colors[shown]))
                {
                    bands[flag] = new FlagColor { Value = colors[shown] };
                }
            }
        }

        /// <summary>Each faction's flag layer: its player's side, 0 (the blank flag) without one.</summary>
        private NativeArray<byte> Layers(AllocatorManager.AllocatorHandle allocator)
        {
            var layers = CollectionHelper.CreateNativeArray<byte>(FactionPalette.Size, allocator);
            using var players = _sides.ToComponentDataArray<Player>(Allocator.Temp);
            using var sides = _sides.ToComponentDataArray<PlayerSide>(Allocator.Temp);
            for (var i = 0; i < players.Length; i++)
            {
                layers[players[i].Faction] = sides[i].Value;
            }

            return layers;
        }
    }
}
