using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// Tints owned meshes with their player's colour (or the <see cref="TeamColorOverride"/>'s), only for new entities,
    /// when the owner changes or when the override does. An entity whose
    /// player hasn't arrived yet (ghosts stream in any order) stays uncoloured and is retried every frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TeamColorSystem : ISystem
    {
        private EntityQuery _uncolored;
        private EntityQuery _recolored;
        private EntityQuery _colored;
        private EntityQuery _changedOverride;
        private int _playerVersion;
        private int _factionVersion;
        private int _overrideVersion;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _uncolored = SystemAPI.QueryBuilder().WithAll<Faction>().WithNone<TeamColored>().Build();
            _recolored = SystemAPI.QueryBuilder().WithAll<Faction, TeamColored>().Build();
            _recolored.SetChangedVersionFilter(ComponentType.ReadOnly<Faction>());
            _colored = SystemAPI.QueryBuilder().WithAll<TeamColored>().Build();
            _changedOverride = SystemAPI.QueryBuilder().WithAll<TeamColorOverride>().Build();
            _changedOverride.SetChangedVersionFilter(ComponentType.ReadOnly<TeamColorOverride>());
            state.RequireForUpdate<Player>();
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (OverrideChanged(ref state))
            {
                // Everything repaints: ColorNewJob picks the roots up again.
                state.CompleteDependency();
                state.EntityManager.RemoveComponent<TeamColored>(_colored);
                _factionVersion = -1;
            }

            if (_recolored.IsEmpty && !HasNewWork(ref state))
            {
                return;
            }

            // Index = faction; w == 0 marks a faction without a player.
            var colors = CollectionHelper.CreateNativeArray<float4>(byte.MaxValue + 1, state.WorldUpdateAllocator);
            foreach (var player in SystemAPI.Query<RefRO<Player>>())
            {
                colors[player.ValueRO.Faction] = new float4(player.ValueRO.Color.xyz, 1f);
            }

            ApplyOverride(ref state, colors);

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

        private bool OverrideChanged(ref SystemState state)
        {
            var order = state.EntityManager.GetComponentOrderVersion<TeamColorOverride>();
            if (order == _overrideVersion && _changedOverride.IsEmpty)
            {
                return false;
            }

            _overrideVersion = order;
            return true;
        }

        /// <summary>Swaps in the override's colour for every listed faction that has a player.</summary>
        private void ApplyOverride(ref SystemState state, NativeArray<float4> colors)
        {
            if (!SystemAPI.TryGetSingleton(out TeamColorOverride colorOverride))
            {
                return;
            }

            foreach (var entry in colorOverride.Colors)
            {
                if (colors[entry.Faction].w != 0f)
                {
                    colors[entry.Faction] = new float4(entry.Color.xyz, 1f);
                }
            }
        }

        // Entities whose player hasn't arrived only get another try once players or owned entities come or go,
        // so a faction that never gets a player doesn't cost a pass every frame.
        private bool HasNewWork(ref SystemState state)
        {
            if (_uncolored.IsEmpty)
            {
                return false;
            }

            var players = state.EntityManager.GetComponentOrderVersion<Player>();
            var owned = state.EntityManager.GetComponentOrderVersion<Faction>();
            if (players == _playerVersion && owned == _factionVersion)
            {
                return false;
            }

            _playerVersion = players;
            _factionVersion = owned;
            return true;
        }

        private struct TeamPainter
        {
            [ReadOnly] public NativeArray<float4> Colors;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Linked;
            [ReadOnly] public BufferLookup<Child> Children;
            [ReadOnly] public ComponentLookup<MaterialMeshInfo> Meshes;
            public EntityCommandBuffer Commands;

            /// <summary>False while the faction has no player yet; neutral entities keep their authored materials.</summary>
            public bool Paint(Entity root, byte faction)
            {
                if (faction == Faction.Neutral)
                {
                    return true;
                }

                var color = Colors[faction];
                if (color.w == 0f)
                {
                    return false;
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

                return true;
            }
        }

        [BurstCompile]
        [WithNone(typeof(TeamColored))]
        private partial struct ColorNewJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in Faction faction)
            {
                if (Painter.Paint(entity, faction.Value))
                {
                    Painter.Commands.AddComponent(entity, new TeamColored { Faction = faction.Value });
                }
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
                if (!Painter.Paint(entity, faction.Value))
                {
                    // The new owner's player hasn't arrived: hand the entity back to ColorNewJob to retry.
                    Painter.Commands.RemoveComponent<TeamColored>(entity);
                }
            }
        }
    }
}
