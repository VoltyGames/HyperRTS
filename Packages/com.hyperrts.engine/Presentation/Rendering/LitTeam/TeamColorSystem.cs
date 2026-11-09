using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>Gives owned meshes their owner's <see cref="TeamColor"/> and the root's <see cref="TeamStrip"/> band for
    /// LitTeam's mask; neutral entities lose them and ones whose player hasn't arrived yet are retried.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct TeamColorSystem : ISystem
    {
        private EntityQuery _uncolored;
        private EntityQuery _recolored;
        private EntityQuery _colored;
        private EntityQuery _changedOverride;
        private EntityQuery _uncoloredSources;
        private ChangeSignature _players;
        private ChangeSignature _owned;
        private ChangeSignature _sources;
        private ChangeSignature _overrides;
        private OwnerPalette _palette;
        private MeshPainter<TeamColor> _colorParts;
        private MeshPainter<TeamStrip> _stripParts;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _uncolored = SystemAPI.QueryBuilder().WithAll<Owner>().WithNone<TeamColored>().Build();
            _colorParts = new MeshPainter<TeamColor>(ref state);
            _stripParts = new MeshPainter<TeamStrip>(ref state);
            _recolored = SystemAPI.QueryBuilder().WithAll<Owner, TeamColored>().Build();
            _recolored.SetChangedVersionFilter(ComponentType.ReadOnly<Owner>());
            _colored = SystemAPI.QueryBuilder().WithAll<TeamColored>().Build();
            _changedOverride = SystemAPI.QueryBuilder().WithAll<TeamColorOverride>().Build();
            _changedOverride.SetChangedVersionFilter(ComponentType.ReadOnly<TeamColorOverride>());
            _uncoloredSources = SystemAPI.QueryBuilder().WithAll<TeamColorSource>().WithNone<TeamColored>().Build();
            _palette = OwnerPalette.Create(ref state);
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
                _owned = default;
            }

            var newWork = HasNewWork(ref state);
            if (_recolored.IsEmpty && !newWork)
            {
                return;
            }

            _colorParts.Update(ref state);
            _stripParts.Update(ref state);
            var painter = new TeamPainter
            {
                Colors = _palette.Build(state.WorldUpdateAllocator),
                ColorParts = _colorParts,
                StripParts = _stripParts,
                Strips = SystemAPI.GetComponentLookup<TeamStripBand>(true),
                Commands = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            };

            state.Dependency = new ColorNewJob { Painter = painter }.Schedule(state.Dependency);
            state.Dependency = new RecolorJob { Painter = painter }.Schedule(state.Dependency);
            state.Dependency = new ColorSourceJob { Painter = painter }.Schedule(state.Dependency);
        }

        private bool OverrideChanged(ref SystemState state) =>
            _overrides.OrderChanged<TeamColorOverride>(ref state) | !_changedOverride.IsEmpty;

        // Entities whose player hasn't arrived only get another try once players, owned entities or sources come or
        // go, so an owner that never gets a player doesn't cost a pass every frame.
        private bool HasNewWork(ref SystemState state)
        {
            if (_uncolored.IsEmpty && _uncoloredSources.IsEmpty)
            {
                return false;
            }

            return _players.OrderChanged<Player>(ref state) | _owned.OrderChanged<Owner>(ref state)
                | _sources.OrderChanged<TeamColorSource>(ref state);
        }

        private struct TeamPainter
        {
            [ReadOnly] public NativeArray<float4> Colors;
            public MeshPainter<TeamColor> ColorParts;
            public MeshPainter<TeamStrip> StripParts;
            [ReadOnly] public ComponentLookup<TeamStripBand> Strips;
            public EntityCommandBuffer Commands;

            /// <summary>False while the owner has no player yet; neutral entities keep their authored materials.</summary>
            public bool Paint(Entity root, byte owner)
            {
                if (owner == Owner.Neutral)
                {
                    return true;
                }

                var color = Colors[owner];
                if (color.w == 0f)
                {
                    return false;
                }

                var targets = new NativeList<Entity>(Allocator.Temp);
                ColorParts.Collect(root, targets);
                ColorParts.Add(targets, new TeamColor { Value = color }, Commands);
                if (Strips.TryGetComponent(root, out var band))
                {
                    StripParts.Add(targets, new TeamStrip { Value = new float4(band.Bottom, band.Top, 0f, 0f) }, Commands);
                }

                return true;
            }

            /// <summary>Gives a root that had a player's colour (and is neutral now) its authored materials back.</summary>
            public void Unpaint(Entity root)
            {
                var targets = new NativeList<Entity>(Allocator.Temp);
                ColorParts.Collect(root, targets);
                ColorParts.Remove(targets, Commands);
                StripParts.Remove(targets, Commands);
            }
        }

        [BurstCompile]
        [WithNone(typeof(TeamColored))]
        private partial struct ColorNewJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in Owner owner)
            {
                if (Painter.Paint(entity, owner.Value))
                {
                    Painter.Commands.AddComponent(entity, new TeamColored { Owner = owner.Value });
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TeamColored))]
        private partial struct ColorSourceJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in TeamColorSource source)
            {
                if (Painter.Paint(entity, source.Owner))
                {
                    Painter.Commands.AddComponent(entity, new TeamColored { Owner = source.Owner });
                }
            }
        }

        [BurstCompile]
        [WithChangeFilter(typeof(Owner))]
        private partial struct RecolorJob : IJobEntity
        {
            public TeamPainter Painter;

            private void Execute(Entity entity, in Owner owner, ref TeamColored colored)
            {
                if (colored.Owner == owner.Value)
                {
                    return;
                }

                colored.Owner = owner.Value;
                if (owner.Value == Owner.Neutral)
                {
                    Painter.Unpaint(entity);
                    return;
                }

                if (!Painter.Paint(entity, owner.Value))
                {
                    // The new owner's player hasn't arrived: hand the entity back to ColorNewJob to retry.
                    Painter.Commands.RemoveComponent<TeamColored>(entity);
                }
            }
        }
    }
}
