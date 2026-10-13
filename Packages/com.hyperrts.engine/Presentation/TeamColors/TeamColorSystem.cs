using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>Gives owned meshes their faction's <see cref="TeamColor"/> and the root's <see cref="TeamStrip"/> band for
    /// LitTeam's mask; neutral entities lose them and ones whose player hasn't arrived yet are retried.</summary>
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
        private FactionPalette _palette;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _uncolored = SystemAPI.QueryBuilder().WithAll<Faction>().WithNone<TeamColored>().Build();
            _recolored = SystemAPI.QueryBuilder().WithAll<Faction, TeamColored>().Build();
            _recolored.SetChangedVersionFilter(ComponentType.ReadOnly<Faction>());
            _colored = SystemAPI.QueryBuilder().WithAll<TeamColored>().Build();
            _changedOverride = SystemAPI.QueryBuilder().WithAll<TeamColorOverride>().Build();
            _changedOverride.SetChangedVersionFilter(ComponentType.ReadOnly<TeamColorOverride>());
            _palette = FactionPalette.Create(ref state);
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

            var painter = new TeamPainter
            {
                Colors = _palette.Build(state.WorldUpdateAllocator),
                Linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
                Meshes = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true),
                Strips = SystemAPI.GetComponentLookup<TeamStripBand>(true),
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
            [ReadOnly] public ComponentLookup<TeamStripBand> Strips;
            public EntityCommandBuffer Commands;

            private static ComponentTypeSet TeamTypes =>
                new(ComponentType.ReadWrite<TeamColor>(), ComponentType.ReadWrite<TeamStrip>());

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

                var hasStrip = Strips.TryGetComponent(root, out var band);
                var targets = new FixedList512Bytes<Entity>();
                RenderHierarchy.Collect(root, Linked, Children, ref targets);
                foreach (var target in targets)
                {
                    if (!Meshes.HasComponent(target))
                    {
                        continue;
                    }

                    if (!hasStrip)
                    {
                        Commands.AddComponent(target, new TeamColor { Value = color });
                        continue;
                    }

                    // One structural change for both, then fill them in.
                    Commands.AddComponent(target, TeamTypes);
                    Commands.SetComponent(target, new TeamColor { Value = color });
                    Commands.SetComponent(target, new TeamStrip { Value = new float4(band.Bottom, band.Top, 0f, 0f) });
                }

                return true;
            }

            /// <summary>Gives a root that had a player's colour (and is neutral now) its authored materials back.</summary>
            public void Unpaint(Entity root)
            {
                var targets = new FixedList512Bytes<Entity>();
                RenderHierarchy.Collect(root, Linked, Children, ref targets);
                foreach (var target in targets)
                {
                    if (Meshes.HasComponent(target))
                    {
                        Commands.RemoveComponent(target, TeamTypes);
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
                if (faction.Value == Faction.Neutral)
                {
                    Painter.Unpaint(entity);
                    return;
                }

                if (!Painter.Paint(entity, faction.Value))
                {
                    // The new owner's player hasn't arrived: hand the entity back to ColorNewJob to retry.
                    Painter.Commands.RemoveComponent<TeamColored>(entity);
                }
            }
        }
    }
}
