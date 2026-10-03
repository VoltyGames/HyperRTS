using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Decides whether a selectable entity is hit by the current <see cref="SelectionInput"/> gesture.</summary>
    public struct SelectionHitTest
    {
        public SelectionInput Input;
        public Entity Clicked;
        public int DoubleClickType;
        public int DoubleClickFaction;

        /// <summary>Local player's faction, or -1 without a local player (then nothing counts as owned).</summary>
        public int LocalFaction;

        /// <summary>Drag boxes keep only entities of this <see cref="SelectionMath.DragRank"/>; -1 keeps all.</summary>
        public int PreferredRank;

        public ComponentLookup<FogHidden> Hidden;
        public ComponentLookup<EntityInfo> Info;
        public ComponentLookup<Faction> Factions;
        public ComponentLookup<UnitTag> Units;
        public ComponentLookup<BuildingTag> Buildings;
        public ComponentLookup<ControlGroup> Groups;

        public bool IsHit(Entity entity, float3 position) => !Hidden.HasComponent(entity) && MatchesGesture(entity, position);

        private bool MatchesGesture(Entity entity, float3 position)
        {
            switch (Input.Command)
            {
                case SelectionCommand.Click:
                    return entity == Clicked;

                case SelectionCommand.DragRelease:
                    return InDragBox(position) && (PreferredRank < 0 || Rank(entity) == PreferredRank);

                case SelectionCommand.DoubleClick:
                    return DoubleClickType != 0 && Info.HasComponent(entity) &&
                           Info[entity].TypeId == DoubleClickType && FactionOf(entity) == DoubleClickFaction &&
                           OnScreen(position);

                case SelectionCommand.RecallGroup:
                    return Groups.HasComponent(entity) && (Groups[entity].Mask & (1 << Input.Group)) != 0;

                default:
                    return false;
            }
        }

        public bool InDragBox(float3 position) =>
            SelectionMath.WorldToScreenPoint(Input.ViewProjection, position, Input.ScreenSize, out var screen) &&
            SelectionMath.RectContains(Input.DragMin, Input.DragMax, screen);

        public int Rank(Entity entity) =>
            SelectionMath.DragRank(IsOwned(entity), Units.HasComponent(entity), Buildings.HasComponent(entity));

        /// <summary>Control groups hold only the local player's entities (anything without a local player).</summary>
        public bool CanJoinGroup(Entity entity) => LocalFaction < 0 || IsOwned(entity);

        /// <summary>The entity's faction, or -1 when it has none.</summary>
        public int FactionOf(Entity entity) => Factions.TryGetComponent(entity, out var faction) ? faction.Value : -1;

        private bool IsOwned(Entity entity) => LocalFaction >= 0 && FactionOf(entity) == LocalFaction;

        private bool OnScreen(float3 position) =>
            SelectionMath.WorldToScreenPoint(Input.ViewProjection, position, Input.ScreenSize, out var screen) &&
            SelectionMath.RectContains(float2.zero, Input.ScreenSize, screen);
    }
}
