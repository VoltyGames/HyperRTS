using System.Collections.Generic;
using HyperRTS.Client.Common;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
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
    /// <summary>
    /// Map overview: the scene's <see cref="MinimapImage"/>, the local fog of war, owner-coloured blips (squares for
    /// structures, dimmed for remembered ones), resource nodes and the camera view; click or drag to move the camera.
    /// </summary>
    public sealed class Minimap : HUDPanel
    {
        private const float MaxViewDistance = 1000f;
        private const float BlipInterval = 0.1f;

        /// <summary>Remembered buildings show at this share of their owner's brightness.</summary>
        private const float MemoryShade = 0.5f;

        private readonly LiveQuery _units = Blips(ComponentType.Exclude<NavObstacle>());
        private readonly LiveQuery _structures = Blips(ComponentType.ReadOnly<NavObstacle>());

        private readonly LiveQuery _memories = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LastSeen>()));

        private readonly LiveQuery _nodes = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ResourceNode>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        private readonly LiveQuery _terrain = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainHeight>()));

        private static readonly Color Outline = new(0f, 0f, 0f, 0.7f);

        private readonly List<(Vector2 Point, Color Color, float Radius, bool Square)> _blips = new();
        private readonly Vector2[] _view = new Vector2[4];
        private readonly VisualElement _canvas;
        private readonly VisualElement _blipLayer;
        private readonly VisualElement _viewLayer;
        private readonly MinimapFog _fog;
        private bool _imageSearched;
        private HUDContext _context;
        private MapSettings _map;
        private bool _hasMap;
        private float _nextBlipTime;

        public Minimap(HUDPanelLayouts layouts) : base(layouts.Minimap)
        {
            _canvas = Root.Require<VisualElement>("canvas");
            _fog = new MinimapFog(Root.Require<VisualElement>("fog"));
            _blipLayer = Root.Require<VisualElement>("blips");
            _blipLayer.generateVisualContent += PaintBlips;
            _canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewLayer = Root.Require<VisualElement>("view");
            _viewLayer.generateVisualContent += PaintView;
        }

        public override bool NeedsPlayer => false;

        public override void Refresh(HUDContext context)
        {
            _context = context;
            var view = context.View;
            _hasMap = view.HasMap;
            _map = view.Map;
            if (_hasMap)
            {
                GatherCameraView(context.EntityManager);
                FindImage();
                _fog.Refresh(context.EntityManager);
            }
            else
            {
                _imageSearched = false;
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
                GatherEntities(view, _units, 2.2f, false);
                GatherEntities(view, _structures, 3.2f, true);
                GatherMemories(view);
            }

            _blipLayer.MarkDirtyRepaint();
        }

        // Units and structures differ only by NavObstacle, which sets their blip size.
        private static LiveQuery Blips(ComponentType obstacle) => new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<EntityInfo>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            obstacle,
            ComponentType.Exclude<FogHidden>(),
            ComponentType.Exclude<Inside>()));

        /// <summary>The scene's map picture, looked up once per match; without one the canvas keeps its plain fill.</summary>
        private void FindImage()
        {
            if (_imageSearched)
            {
                return;
            }

            _imageSearched = true;
            var image = Object.FindAnyObjectByType<MinimapImage>();
            _canvas.style.backgroundImage = image != null && image.image != null
                ? new StyleBackground(image.image)
                : new StyleBackground(StyleKeyword.Null);
        }

        private void GatherNodes(EntityManager entityManager)
        {
            var query = _nodes.In(entityManager);
            using var nodes = query.ToComponentDataArray<ResourceNode>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < nodes.Length; i++)
            {
                var type = nodes[i].Type.Value;
                AddBlip(transforms[i].Position.xz, type != null ? type.color : Color.white, 2.4f, true);
            }
        }

        private void GatherEntities(MatchView view, LiveQuery liveQuery, float radius, bool square)
        {
            var query = liveQuery.In(view.EntityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < factions.Length; i++)
            {
                AddBlip(transforms[i].Position.xz, view.GetColor(factions[i].Value), radius, square);
            }
        }

        private void GatherMemories(MatchView view)
        {
            using var memories = _memories.In(view.EntityManager).ToComponentDataArray<LastSeen>(Allocator.Temp);
            foreach (var memory in memories)
            {
                var color = view.GetColor(memory.State.Faction);
                var shaded = new Color(color.r * MemoryShade, color.g * MemoryShade, color.b * MemoryShade, color.a);
                AddBlip(memory.State.Transform.Position.xz, shaded, 3.2f, true);
            }
        }

        private void GatherCameraView(EntityManager entityManager)
        {
            var camera = Camera.main;
            _terrain.In(entityManager).TryGetSingleton(out TerrainHeight terrain);
            for (var i = 0; i < _view.Length; i++)
            {
                var ground = float3.zero;
                if (camera != null)
                {
                    var corner = new Vector3(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f, 0f);
                    var ray = camera.ViewportPointToRay(corner);
                    ground = ViewGround.Reach(terrain, ray.origin, ray.direction, MaxViewDistance);
                }

                _view[i] = MinimapMath.WorldToMinimap(ground.xz, _map.Min, _map.Size);
            }
        }

        private void AddBlip(float2 world, Color color, float radius, bool square) =>
            _blips.Add((MinimapMath.WorldToMinimap(world, _map.Min, _map.Size), color, radius, square));

        private void PaintBlips(MeshGenerationContext context)
        {
            if (!_hasMap)
            {
                return;
            }

            var size = _blipLayer.contentRect.size;
            var painter = context.painter2D;
            painter.strokeColor = Outline;
            painter.lineWidth = 1f;
            foreach (var (point, color, radius, square) in _blips)
            {
                var center = Vector2.Scale(point, size);
                painter.fillColor = color;
                painter.BeginPath();
                if (square)
                {
                    painter.MoveTo(center + new Vector2(-radius, -radius));
                    painter.LineTo(center + new Vector2(radius, -radius));
                    painter.LineTo(center + new Vector2(radius, radius));
                    painter.LineTo(center + new Vector2(-radius, radius));
                    painter.ClosePath();
                }
                else
                {
                    painter.Arc(center, radius, 0f, 360f);
                }

                painter.Fill();
                painter.Stroke();
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

        // The camera moves itself (keeping height, angle and the map bounds) when it reads the request.
        private void Focus(Vector2 local)
        {
            var size = _canvas.contentRect.size;
            var hasArea = size.x > 0f && size.y > 0f;
            if (!_hasMap || !hasArea || _context == null)
            {
                return;
            }

            var target = MinimapMath.MinimapToWorld(new Vector2(local.x / size.x, local.y / size.y), _map.Min, _map.Size);
            _context.FocusCamera(new float3(target.x, 0f, target.y));
        }
    }
}
