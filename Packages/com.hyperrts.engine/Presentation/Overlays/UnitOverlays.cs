using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Veterancy;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Ground markers under selected and hovered entities, in relation colours, and health bars over the ones the
    /// visibility setting picks. Both read the drawn pose, so they stay on moving models.
    /// </summary>
    public sealed class UnitOverlays : IDisposable
    {
        private readonly LiveQuery _living = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Health>(),
            ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>(),
            ComponentType.Exclude<Inside>()));

        private readonly LiveSingleton<HoveredEntity> _hovered = new();
        private readonly LiveSingleton<SquadRules> _squadRules = new();

        private readonly GroundMarkers _markers;
        private readonly HealthBars _bars;
        private readonly SelectionFades _fades = new();

        public UnitOverlays(Mesh grid, Mesh billboard, Material markerMaterial, Material barMaterial)
        {
            _markers = new GroundMarkers(grid, markerMaterial);
            _bars = new HealthBars(billboard, barMaterial);
        }

        public void Gather(MatchView view, OverlayStyle style, HealthBarVisibility visibility, float now)
        {
            var entityManager = view.EntityManager;
            var handles = new UnitHandles(entityManager);
            _hovered.TryGet(entityManager, out var hovered);
            _squadRules.TryGet(entityManager, out var squadRules);
            using var chunks = _living.In(entityManager).ToArchetypeChunkArray(Allocator.Temp);
            foreach (var chunk in chunks)
            {
                var entities = chunk.GetNativeArray(handles.Entities);
                var owners = chunk.GetNativeArray(ref handles.Owners);
                var healths = chunk.GetNativeArray(ref handles.Healths);
                var worlds = chunk.GetNativeArray(ref handles.Worlds);
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (handles.IsInside(chunk, i))
                    {
                        continue;
                    }

                    var health = healths[i];
                    var focus = new OverlayFocus(handles.IsSelected(chunk, i), entities[i] == hovered.Value,
                        health.Current < health.Max);
                    var showsBar = focus.ShowsBar(visibility);
                    if (!focus.ShowsMarker && !showsBar)
                    {
                        continue;
                    }

                    var unit = handles.Unit(chunk, i);
                    if (focus.ShowsMarker)
                    {
                        var color = style.RelationColor(view.RelationTo(owners[i].Value));
                        var pose = new MarkerPose(worlds[i], unit.Layer);
                        var footprint = MarkerSize(unit, style);
                        var selectedFor = focus.Selected ? _fades.Elapsed(entities[i], now) : 0f;
                        AddMarker(pose, footprint, color, focus.Selected, selectedFor, style);
                    }

                    if (showsBar)
                    {
                        AddBar(entityManager, entities[i], unit, worlds[i].Position, health, squadRules, style);
                    }
                }
            }

            _fades.EndFrame();
        }

        public void Draw(OverlayStyle style, float uiScale, int layer)
        {
            _markers.Draw(layer);
            _bars.Draw(style, uiScale, layer);
        }

        public void Dispose()
        {
            _markers.Dispose();
            _bars.Dispose();
        }

        private static MarkerFootprint MarkerSize(in OverlayUnit unit, OverlayStyle style)
        {
            return unit.IsSquad
                ? MarkerFootprint.Round(unit.Spread, style.markerMargin)
                : unit.Footprint(style.markerMargin);
        }

        private void AddMarker(in MarkerPose pose, MarkerFootprint footprint, Color color, bool selected, float selectedFor,
            OverlayStyle style)
        {
            if (selected)
            {
                var settle = SelectionFades.ScaleAndAlpha(selectedFor, style.selectSeconds, style.selectStartScale);
                footprint = footprint.Scaled(settle.x);
                color.a *= settle.y;
            }
            else
            {
                color.a *= style.hoverOpacity;
            }

            _markers.Add(pose, footprint, color, selected, style);
        }

        private void AddBar(EntityManager entityManager, Entity entity, in OverlayUnit unit, float3 position,
            in Health health, in SquadRules squadRules, OverlayStyle style)
        {
            var top = EntityExtent.Top(entityManager, entity, position, unit.Radius);
            var bar = new HealthBarData(health, unit.MemberCount, unit.Alive, unit.Rank, unit.Radius,
                SquadBarState.Read(unit, squadRules), style);
            _bars.Add(new Vector3(position.x, top, position.z), bar, style);
        }

        // Chunk reads skip the job sync EntityManager.GetComponentData does, so the constructor completes writers first.
        private struct UnitHandles
        {
            public readonly EntityTypeHandle Entities;
            public ComponentTypeHandle<Owner> Owners;
            public ComponentTypeHandle<Health> Healths;
            public ComponentTypeHandle<LocalToWorld> Worlds;
            private ComponentTypeHandle<Selected> _selected;
            private ComponentTypeHandle<Inside> _inside;
            private ComponentTypeHandle<NavAgent> _agents;
            private ComponentTypeHandle<NavObstacle> _obstacles;
            private ComponentTypeHandle<Experience> _experience;
            private BufferTypeHandle<SquadMember> _members;
            private ComponentTypeHandle<Suppression> _suppression;
            private ComponentTypeHandle<Retreating> _retreating;

            public UnitHandles(EntityManager entityManager)
            {
                Entities = entityManager.GetEntityTypeHandle();
                Owners = Read<Owner>(entityManager);
                Healths = Read<Health>(entityManager);
                Worlds = Read<LocalToWorld>(entityManager);
                _selected = Read<Selected>(entityManager);
                _inside = Read<Inside>(entityManager);
                _agents = Read<NavAgent>(entityManager);
                _obstacles = Read<NavObstacle>(entityManager);
                _experience = Read<Experience>(entityManager);
                _suppression = Read<Suppression>(entityManager);
                _retreating = Read<Retreating>(entityManager);
                entityManager.CompleteDependencyBeforeRO<SquadMember>();
                _members = entityManager.GetBufferTypeHandle<SquadMember>(true);
            }

            public bool IsSelected(in ArchetypeChunk chunk, int index) =>
                chunk.Has(ref _selected) && chunk.IsComponentEnabled(ref _selected, index);

            // The query's Exclude<Inside> keeps whole chunks; an enabled bit has to be checked per entity.
            public bool IsInside(in ArchetypeChunk chunk, int index) =>
                chunk.Has(ref _inside) && chunk.IsComponentEnabled(ref _inside, index);

            public OverlayUnit Unit(in ArchetypeChunk chunk, int index) => new(
                chunk.Has(ref _agents) ? chunk.GetNativeArray(ref _agents)[index] : null,
                chunk.Has(ref _obstacles) ? chunk.GetNativeArray(ref _obstacles)[index] : null,
                chunk.Has(ref _experience) ? chunk.GetNativeArray(ref _experience)[index].Rank : 0,
                chunk.Has(ref _members) ? chunk.GetBufferAccessor(ref _members)[index] : default,
                chunk.Has(ref _suppression) ? chunk.GetNativeArray(ref _suppression)[index] : null,
                chunk.Has(ref _retreating) && chunk.IsComponentEnabled(ref _retreating, index));

            private static ComponentTypeHandle<T> Read<T>(EntityManager entityManager) where T : unmanaged, IComponentData
            {
                entityManager.CompleteDependencyBeforeRO<T>();
                return entityManager.GetComponentTypeHandle<T>(true);
            }
        }
    }
}
