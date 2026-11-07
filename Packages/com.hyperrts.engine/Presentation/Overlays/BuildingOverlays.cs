using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Vision.Fog;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Over buildings, always on top and the same size at every zoom: a capture dial filling in the capturer's colour
    /// while a capture is under way, and a pip per garrison slot, filled in the holder's colour, while anyone is
    /// inside. Hidden where the building is fogged.
    /// </summary>
    public sealed class BuildingOverlays : IDisposable
    {
        private const int DialSteps = 64;
        private const float DialStepDegrees = 360f / DialSteps;
        private const float DialInner = 0.7f;
        private const float BackRim = 1.2f;
        private const float PipPitch = 1.5f;

        // Marker sizes are pixels at this screen height, so they keep their share of the screen at any resolution.
        private const float ReferenceHeight = 1080f;

        private static readonly Quaternion[] DialRotations = BuildDialRotations();

        private readonly LiveQuery _captures = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CaptureProgress>(),
            ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveQuery _garrisons = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Container>(),
            ComponentType.ReadOnly<Cargo>(),
            ComponentType.ReadOnly<Owner>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.ReadOnly<BuildingTag>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveSingleton<FogOfWar> _fogGrid = new();
        private readonly LiveSingleton<LocalFogView> _fogView = new();

        private readonly InstanceBuffer<OverlayInstance> _dialBacks;
        private readonly InstanceBuffer<OverlayInstance> _dialSteps;
        private readonly InstanceBuffer<OverlayInstance> _pipBacks;
        private readonly InstanceBuffer<OverlayInstance> _pips;
        private LocalFogView _view;
        private FogOfWar _fog;
        private bool _fogRead;
        private Vector3 _cameraPosition;
        private Vector3 _cameraForward;
        private Vector3 _cameraRight;
        private Quaternion _cameraRotation;
        private bool _orthographic;
        private float _orthographicPixel;
        private float _pixelPerDepth;

        public BuildingOverlays(Mesh dialSegment, Mesh disc, Mesh billboard)
        {
            _dialBacks = new InstanceBuffer<OverlayInstance>(disc);
            _dialSteps = new InstanceBuffer<OverlayInstance>(dialSegment);
            _pipBacks = new InstanceBuffer<OverlayInstance>(billboard);
            _pips = new InstanceBuffer<OverlayInstance>(billboard);
        }

        public static Mesh DialSegment() => OverlayMeshes.DialSegment(DialSteps, DialInner);

        public void Gather(MatchView view, OverlayStyle style, Camera camera)
        {
            _fogRead = false;
            ReadCamera(camera);
            GatherDials(view, style);
            GatherPips(view, style);
        }

        // Backings first, then rings and pips over them; neither tests depth, so buildings never cut through them.
        public void Draw(OverlayMaterials materials, int layer)
        {
            _dialBacks.Draw(materials.OnTop, layer);
            _pipBacks.Draw(materials.OnTop, layer);
            _dialSteps.Draw(materials.OnTopFront, layer);
            _pips.Draw(materials.OnTopFront, layer);
        }

        public void Dispose()
        {
            _dialBacks.Dispose();
            _dialSteps.Dispose();
            _pipBacks.Dispose();
            _pips.Dispose();
        }

        private void GatherDials(MatchView view, OverlayStyle style)
        {
            var entityManager = view.EntityManager;
            var query = _captures.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var captures = query.ToComponentDataArray<CaptureProgress>(Allocator.Temp);
            using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var capture = captures[i];
                var owner = owners[i].Value;
                var position = transforms[i].Position;
                if (!capture.IsUnderWay(owner) || IsHidden(view, owner, position))
                {
                    continue;
                }

                var top = Top(entityManager, entities[i], position);
                var pixel = PixelSize(top);
                var center = top + Vector3.up * (pixel * (style.markerOffset + style.dialRadius * BackRim));
                AddDial(center, view.GetColor(capture.Owner), capture.Value, pixel * style.dialRadius, style);
            }
        }

        private void AddDial(Vector3 center, Color capturer, float progress, float radius, OverlayStyle style)
        {
            var back = radius * BackRim;
            _dialBacks.Add(new OverlayInstance(Matrix4x4.TRS(center, _cameraRotation, new Vector3(back, back, 1f)), style.markerBack));
            var filled = Mathf.RoundToInt(Mathf.Clamp01(progress) * DialSteps);
            var scale = new Vector3(radius, radius, 1f);
            for (var step = 0; step < DialSteps; step++)
            {
                var matrix = Matrix4x4.TRS(center, _cameraRotation * DialRotations[step], scale);
                var color = step < filled ? capturer : style.dialTrack;
                _dialSteps.Add(new OverlayInstance(matrix, color));
            }
        }

        private void GatherPips(MatchView view, OverlayStyle style)
        {
            var entityManager = view.EntityManager;
            var query = _garrisons.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var containers = query.ToComponentDataArray<Container>(Allocator.Temp);
            using var owners = query.ToComponentDataArray<Owner>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var used = Container.Used(entityManager.GetBuffer<Cargo>(entities[i], true));
                var owner = owners[i].Value;
                var position = transforms[i].Position;
                if (used == 0 || IsHidden(view, owner, position))
                {
                    continue;
                }

                var top = Top(entityManager, entities[i], position);
                var pixel = PixelSize(top);
                var center = top + Vector3.up * (pixel * (style.markerOffset + style.pipSize));
                AddPips(center, view.GetColor(owner), used, containers[i].Capacity, pixel * style.pipSize, style);
            }
        }

        private void AddPips(Vector3 center, Color owner, int used, int capacity, float size, OverlayStyle style)
        {
            var pitch = size * PipPitch;
            var backSize = new Vector3(pitch * capacity + size * 0.5f, size * 2f, 1f);
            _pipBacks.Add(new OverlayInstance(Matrix4x4.TRS(center, _cameraRotation, backSize), style.markerBack));
            var first = center - _cameraRight * (pitch * (capacity - 1) * 0.5f);
            var scale = new Vector3(size, size, 1f);
            for (var slot = 0; slot < capacity; slot++)
            {
                var matrix = Matrix4x4.TRS(first + _cameraRight * (pitch * slot), _cameraRotation, scale);
                var color = slot < used ? owner : style.pipEmpty;
                _pips.Add(new OverlayInstance(matrix, color));
            }
        }

        private void ReadCamera(Camera camera)
        {
            var transform = camera.transform;
            _cameraPosition = transform.position;
            _cameraForward = transform.forward;
            _cameraRight = transform.right;
            _cameraRotation = transform.rotation;
            _orthographic = camera.orthographic;
            _orthographicPixel = camera.orthographicSize * 2f / ReferenceHeight;
            _pixelPerDepth = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / ReferenceHeight;
        }

        /// <summary>World units one reference pixel covers at <paramref name="point"/>.</summary>
        private float PixelSize(Vector3 point)
        {
            if (_orthographic)
            {
                return _orthographicPixel;
            }

            return Vector3.Dot(point - _cameraPosition, _cameraForward) * _pixelPerDepth;
        }

        private static Quaternion[] BuildDialRotations()
        {
            var rotations = new Quaternion[DialSteps];
            for (var step = 0; step < DialSteps; step++)
            {
                rotations[step] = Quaternion.AngleAxis(-step * DialStepDegrees, Vector3.forward);
            }

            return rotations;
        }

        private static Vector3 Top(EntityManager entityManager, Entity entity, float3 position)
        {
            var radius = EntityRadius.Get(entityManager, entity);
            return new Vector3(position.x, EntityExtent.Top(entityManager, entity, position, radius), position.z);
        }

        private bool IsHidden(MatchView view, byte owner, float3 position)
        {
            if (!_fogRead)
            {
                ReadFog(view.EntityManager);
            }

            return BuildingFog.IsHidden(in _view, in _fog, view.Relations, owner, position);
        }

        // Only once a building needs it: simulation jobs write the grid, so reading it completes them.
        private void ReadFog(EntityManager entityManager)
        {
            _fogView.TryGet(entityManager, out _view);
            _fogGrid.Complete(entityManager);
            _fogGrid.TryGet(entityManager, out _fog);
            _fogRead = true;
        }
    }
}
