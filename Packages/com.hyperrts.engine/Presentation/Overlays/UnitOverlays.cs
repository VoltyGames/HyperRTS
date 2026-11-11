using HyperRTS.Presentation.Overlays.Bars;
using HyperRTS.Presentation.Overlays.Markers;
using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
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
            var pass = new GatherPass(view, style, visibility, now, hovered.Value, squadRules);
            using var chunks = _living.In(entityManager).ToArchetypeChunkArray(Allocator.Temp);
            foreach (var chunk in chunks)
            {
                GatherChunk(pass, ref handles, chunk);
            }

            _fades.EndFrame();
        }

        private void GatherChunk(in GatherPass pass, ref UnitHandles handles, in ArchetypeChunk chunk)
        {
            var entities = chunk.GetNativeArray(handles.Entities);
            var owners = chunk.GetNativeArray(ref handles.Owners);
            var healths = chunk.GetNativeArray(ref handles.Healths);
            var worlds = chunk.GetNativeArray(ref handles.Worlds);
            var units = new ChunkUnits(chunk, ref handles);
            for (var i = 0; i < chunk.Count; i++)
            {
                if (handles.IsInside(chunk, i))
                {
                    continue;
                }

                var health = healths[i];
                var focus = new OverlayFocus(handles.IsSelected(chunk, i), entities[i] == pass.Hovered,
                    health.Current < health.Max);
                var showsBar = focus.ShowsBar(pass.Visibility);
                if (!focus.ShowsMarker && !showsBar)
                {
                    continue;
                }

                var unit = units.Unit(i);
                if (focus.ShowsMarker)
                {
                    var color = pass.Style.RelationColor(pass.View.RelationTo(owners[i].Value));
                    var pose = new MarkerPose(worlds[i], unit.Layer);
                    var seconds = focus.Selected ? _fades.Elapsed(entities[i], pass.Now) : 0f;
                    AddMarker(pose, MarkerSize(unit, pass.Style), color, focus.Selected, seconds, pass.Style);
                }

                if (showsBar)
                {
                    var position = worlds[i].Position;
                    var top = EntityExtent.Top(pass.View.EntityManager, entities[i], position, unit.Radius);
                    AddBar(unit, new Vector3(position.x, top, position.z), health, pass.Rules, pass.Style);
                }
            }
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

        private void AddBar(in OverlayUnit unit, Vector3 anchor, in Health health, in SquadRules squadRules,
            OverlayStyle style)
        {
            var bar = new HealthBarData(health, unit.Rank, unit.Radius, SquadBarState.Read(unit, squadRules), style);
            _bars.Add(anchor, bar, style);
        }

        private readonly struct GatherPass
        {
            public readonly MatchView View;
            public readonly OverlayStyle Style;
            public readonly HealthBarVisibility Visibility;
            public readonly float Now;
            public readonly Entity Hovered;
            public readonly SquadRules Rules;

            public GatherPass(MatchView view, OverlayStyle style, HealthBarVisibility visibility, float now,
                Entity hovered, SquadRules rules)
            {
                View = view;
                Style = style;
                Visibility = visibility;
                Now = now;
                Hovered = hovered;
                Rules = rules;
            }
        }
    }
}
