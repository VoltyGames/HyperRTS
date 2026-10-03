using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>Tints owned meshes with their player's colour, only for new entities or when the owner changes.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TeamColorSystem : ISystem
    {
        private EntityQuery _uncolored;
        private EntityQuery _recolored;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _uncolored = SystemAPI.QueryBuilder().WithAll<Faction>().WithNone<TeamColored>().Build();
            _recolored = SystemAPI.QueryBuilder().WithAll<Faction, TeamColored>().Build();
            _recolored.SetChangedVersionFilter(ComponentType.ReadOnly<Faction>());
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_uncolored.IsEmpty && _recolored.IsEmpty)
            {
                return;
            }

            // Index = faction; w == 0 marks a faction without a player.
            var colors = CollectionHelper.CreateNativeArray<float4>(byte.MaxValue + 1, state.WorldUpdateAllocator);
            foreach (var player in SystemAPI.Query<RefRO<Player>>())
            {
                colors[player.ValueRO.Faction] = new float4(player.ValueRO.Color.xyz, 1f);
            }

            var painter = new TeamPainter
            {
                Colors = colors,
                Linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
                Meshes = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true),
                Commands = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };

            state.Dependency = new ColorNewJob { Painter = painter }.Schedule(state.Dependency);
            state.Dependency = new RecolorJob { Painter = painter }.Schedule(state.Dependency);
        }

        private struct TeamPainter
        {
            [ReadOnly] public NativeArray<float4> Colors;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Linked;
            [ReadOnly] public BufferLookup<Child> Children;
            [ReadOnly] public ComponentLookup<MaterialMeshInfo> Meshes;
            public EntityCommandBuffer Commands;

            // Neutral and player-less factions keep their authored materials.
            public void Paint(Entity root, byte faction)
            {
                var color = Colors[faction];
                if (faction == Faction.Neutral || color.w == 0f)
                {
                    return;
                }

                var targets = new FixedList512Bytes<Entity>();
                RenderHierarchy.Collect(root, Linked, Children, ref targets);
                foreach (var target in targets)
                {
                    if (Meshes.HasComponent(target))
                    {
                        Commands.AddComponent(target, new URPMaterialPropertyBaseColor { Value = color });
                    }
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TeamColored))]
        private partial struct ColorNewJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in Faction faction)
            {
                Painter.Commands.AddComponent(entity, new TeamColored { Faction = faction.Value });
                Painter.Paint(entity, faction.Value);
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(Faction))]
        private partial struct RecolorJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in Faction faction, ref TeamColored colored)
            {
                if (colored.Faction == faction.Value)
                {
                    return;
                }

                colored.Faction = faction.Value;
                Painter.Paint(entity, faction.Value);
            }
        }
    }
}
