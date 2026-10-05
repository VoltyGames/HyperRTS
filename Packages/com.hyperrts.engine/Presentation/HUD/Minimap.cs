using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Map overview: owner-coloured blips, resource nodes, the camera view; click or drag to move the camera.</summary>
    public sealed class Minimap
    {
        private const float GroundHeight = 0f;
        private const float MaxViewDistance = 1000f;
        private const float BlipInterval = 0.1f;

        private readonly LiveQuery _units = Blips(ComponentType.Exclude<NavObstacle>());
        private readonly LiveQuery _structures = Blips(ComponentType.ReadOnly<NavObstacle>());

        private readonly LiveQuery _nodes = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ResourceNode>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        private readonly List<(Vector2 Point, Color Color, float Radius)> _blips = new();
        private readonly Vector2[] _view = new Vector2[4];
        private readonly VisualElement _canvas;
        private readonly VisualElement _viewLayer;
        private MapSettings _map;
        private bool _hasMap;
        private float _nextBlipTime;

        public Minimap()
        {
            Root = HUDElements.Box("hud-minimap");
            Root.AddToClassList("hud-panel");
            _canvas = HUDElements.Box("hud-minimap__canvas", Root);
            _canvas.generateVisualContent += PaintBlips;
            _canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewLayer = HUDElements.Box("hud-minimap__view", _canvas);
            _viewLayer.pickingMode = PickingMode.Ignore;
            _viewLayer.generateVisualContent += PaintView;
        }

        public VisualElement Root { get; }

        public void Refresh(HUDContext context)
        {
            var view = context.View;
            _hasMap = view.HasMap;
            _map = view.Map;
            if (_hasMap)
            {
                GatherCameraView();
            }

            _viewLayer.MarkDirtyRepaint();

            // Repainting re-tessellates every blip, so blips refresh at a fixed rate while the view rect stays live.
            if (Time.unscaledTime < _nextBlipTime)
            {
                return;
            }

            _nextBlipTime = Time.unscaledTime + BlipInterval;
            _blips.Clear();
            if (_hasMap)
            {
                GatherNodes(context.EntityManager);
                GatherEntities(view, _units, 2.5f);
                GatherEntities(view, _structures, 4f);
            }

            _canvas.MarkDirtyRepaint();
        }

        // Units and structures differ only by NavObstacle, which sets their blip size.
        private static LiveQuery Blips(ComponentType obstacle) => new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EntityInfo>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            obstacle,
            ComponentType.Exclude<FogHidden>()));

        private void GatherNodes(EntityManager entityManager)
        {
            var query = _nodes.In(entityManager);
            using var nodes = query.ToComponentDataArray<ResourceNode>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < nodes.Length; i++)
            {
                var type = nodes[i].Type.Value;
                AddBlip(transforms[i].Position.xz, type != null ? type.color : Color.white, 3f);
            }
        }

        private void GatherEntities(MatchView view, LiveQuery liveQuery, float radius)
        {
            var query = liveQuery.In(view.EntityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < factions.Length; i++)
            {
                AddBlip(transforms[i].Position.xz, view.ColorOf(factions[i].Value), radius);
            }
        }

        private void GatherCameraView()
        {
            var camera = Camera.main;
            for (var i = 0; i < _view.Length; i++)
            {
                var corner = new Vector3(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f, 0f);
                var ground = camera != null
                    ? MinimapMath.GroundPoint(camera.ViewportPointToRay(corner), GroundHeight, MaxViewDistance)
                    : Vector3.zero;
                _view[i] = MinimapMath.WorldToMinimap(new float2(ground.x, ground.z), _map.Min, _map.Size);
            }
        }

        private void AddBlip(float2 world, Color color, float radius) =>
            _blips.Add((MinimapMath.WorldToMinimap(world, _map.Min, _map.Size), color, radius));

        private void PaintBlips(MeshGenerationContext context)
        {
            if (!_hasMap)
            {
                return;
            }

            var size = _canvas.contentRect.size;
            var painter = context.painter2D;
            foreach (var (point, color, radius) in _blips)
            {
                painter.fillColor = color;
                painter.BeginPath();
                painter.Arc(Vector2.Scale(point, size), radius, 0f, 360f);
                painter.Fill();
            }
        }

        private void PaintView(MeshGenerationContext context)
        {
            if (!_hasMap)
            {
                return;
            }

            var size = _viewLayer.contentRect.size;
            var painter = context.painter2D;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.85f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(Vector2.Scale(_view[0], size));
            for (var i = 1; i < _view.Length; i++)
            {
                painter.LineTo(Vector2.Scale(_view[i], size));
            }

            painter.ClosePath();
            painter.Stroke();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _canvas.CapturePointer(evt.pointerId);
            Focus(evt.localPosition);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_canvas.HasPointerCapture(evt.pointerId))
            {
                Focus(evt.localPosition);
            }
        }

        private void OnPointerUp(PointerUpEvent evt) => _canvas.ReleasePointer(evt.pointerId);

        // Shifts the camera so its look point lands on the clicked spot, keeping height and angle.
        private void Focus(Vector2 local)
        {
            var camera = Camera.main;
            var size = _canvas.contentRect.size;
            if (!_hasMap || camera == null || size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            var target = MinimapMath.MinimapToWorld(new Vector2(local.x / size.x, local.y / size.y), _map.Min, _map.Size);
            var transform = camera.transform;
            var focus = MinimapMath.GroundPoint(new Ray(transform.position, transform.forward), GroundHeight, MaxViewDistance);
            transform.position += new Vector3(target.x - focus.x, 0f, target.y - focus.z);
        }
    }
}
