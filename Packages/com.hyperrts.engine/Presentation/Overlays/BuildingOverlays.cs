using HyperRTS.Client.Common;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
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
    public sealed class BuildingOverlays
    {
        private const int DialSteps = 64;
        private const float DialStepDegrees = 360f / DialSteps;
        private const float DialInner = 0.7f;
        private const float BackRim = 1.2f;
        private const float PipPitch = 1.5f;

        // Marker sizes are pixels at this screen height, so they keep their share of the screen at any resolution.
        private const float ReferenceHeight = 1080f;

        private readonly LiveQuery _captures = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CaptureProgress>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveQuery _garrisons = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Container>(),
            ComponentType.ReadOnly<Cargo>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.ReadOnly<BuildingTag>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveQuery _fogGrid = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<FogOfWar>()));

        private readonly LiveQuery _fogView = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalFogView>()));

        private readonly InstanceBatch _dialBacks;
        private readonly InstanceBatch _dialTrack;
        private readonly FactionBatches _dialFills;
        private readonly InstanceBatch _pipBacks;
        private readonly InstanceBatch _pipEmpty;
        private readonly FactionBatches _pipFills;
        private LocalFogView _view;
        private FogOfWar _fog;

        public BuildingOverlays(Mesh dialSegment, Mesh disc, Mesh billboard)
        {
            _dialBacks = new InstanceBatch(disc);
            _dialTrack = new InstanceBatch(dialSegment);
            _dialFills = new FactionBatches(dialSegment);
            _pipBacks = new InstanceBatch(billboard);
            _pipEmpty = new InstanceBatch(billboard);
            _pipFills = new FactionBatches(billboard);
        }

        public static Mesh DialSegment() => OverlayMeshes.DialSegment(DialSteps, DialInner);

        public void Gather(MatchView view, OverlayStyle style, Camera camera)
        {
            ReadFog(view.EntityManager);
            GatherDials(view, style, camera);
            GatherPips(view, style, camera);
        }

        // Backings first, then rings and pips over them; neither tests depth, so buildings never cut through them.
        public void Draw(OverlayMaterials materials, MatchView view, OverlayStyle style, int layer)
        {
            _dialBacks.Draw(materials.OnTop, style.markerBack, layer);
            _pipBacks.Draw(materials.OnTop, style.markerBack, layer);
            _dialTrack.Draw(materials.OnTopFront, style.dialTrack, layer);
            _dialFills.Draw(materials.OnTopFront, view, layer);
            _pipEmpty.Draw(materials.OnTopFront, style.pipEmpty, layer);
            _pipFills.Draw(materials.OnTopFront, view, layer);
        }

        private void GatherDials(MatchView view, OverlayStyle style, Camera camera)
        {
            var entityManager = view.EntityManager;
            var query = _captures.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var captures = query.ToComponentDataArray<CaptureProgress>(Allocator.Temp);
            using var owners = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var capture = captures[i];
                var owner = owners[i].Value;
                var position = transforms[i].Position;
                var underWay = capture.Faction != Faction.Neutral && capture.Faction != owner && capture.Value > 0f;
                if (!underWay || IsHidden(view, owner, position))
                {
                    continue;
                }

                var top = Top(entityManager, entities[i], position);
                var pixel = PixelSize(camera, top);
                var center = top + Vector3.up * (pixel * (style.markerOffset + style.dialRadius * BackRim));
                AddDial(center, capture.Faction, capture.Value, pixel * style.dialRadius, camera.transform.rotation);
            }
        }

        private void AddDial(Vector3 center, byte capturer, float progress, float radius, Quaternion facing)
        {
            var back = radius * BackRim;
            _dialBacks.Add(Matrix4x4.TRS(center, facing, new Vector3(back, back, 1f)));
            var filled = Mathf.RoundToInt(Mathf.Clamp01(progress) * DialSteps);
            var scale = new Vector3(radius, radius, 1f);
            for (var step = 0; step < DialSteps; step++)
            {
                var rotation = facing * Quaternion.AngleAxis(-step * DialStepDegrees, Vector3.forward);
                var matrix = Matrix4x4.TRS(center, rotation, scale);
                if (step < filled)
                {
                    _dialFills.Add(capturer, matrix);
                }
                else
                {
                    _dialTrack.Add(matrix);
                }
            }
        }

        private void GatherPips(MatchView view, OverlayStyle style, Camera camera)
        {
            var entityManager = view.EntityManager;
            var query = _garrisons.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var containers = query.ToComponentDataArray<Container>(Allocator.Temp);
            using var owners = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var used = Occupied(entityManager.GetBuffer<Cargo>(entities[i], true));
                var owner = owners[i].Value;
                var position = transforms[i].Position;
                if (used == 0 || IsHidden(view, owner, position))
                {
                    continue;
                }

                var top = Top(entityManager, entities[i], position);
                var pixel = PixelSize(camera, top);
                var center = top + Vector3.up * (pixel * (style.markerOffset + style.pipSize));
                AddPips(center, owner, used, containers[i].Capacity, pixel * style.pipSize, camera.transform);
            }
        }

        private void AddPips(Vector3 center, byte owner, int used, int capacity, float size, Transform camera)
        {
            var pitch = size * PipPitch;
            var backSize = new Vector3(pitch * capacity + size * 0.5f, size * 2f, 1f);
            _pipBacks.Add(Matrix4x4.TRS(center, camera.rotation, backSize));
            var first = center - camera.right * (pitch * (capacity - 1) * 0.5f);
            var scale = new Vector3(size, size, 1f);
            for (var slot = 0; slot < capacity; slot++)
            {
                var matrix = Matrix4x4.TRS(first + camera.right * (pitch * slot), camera.rotation, scale);
                if (slot < used)
                {
                    _pipFills.Add(owner, matrix);
                }
                else
                {
                    _pipEmpty.Add(matrix);
                }
            }
        }

        /// <summary>World units one reference pixel covers at <paramref name="point"/>.</summary>
        private static float PixelSize(Camera camera, Vector3 point)
        {
            if (camera.orthographic)
            {
                return camera.orthographicSize * 2f / ReferenceHeight;
            }

            var depth = Vector3.Dot(point - camera.transform.position, camera.transform.forward);
            return depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / ReferenceHeight;
        }

        private static Vector3 Top(EntityManager entityManager, Entity entity, float3 position)
        {
            var radius = EntityRadius.Get(entityManager, entity);
            return new Vector3(position.x, EntityExtent.Top(entityManager, entity, position, radius), position.z);
        }

        private static int Occupied(DynamicBuffer<Cargo> cargo)
        {
            var used = 0;
            foreach (var item in cargo)
            {
                used += item.Size;
            }

            return used;
        }

        // The grid is written by simulation jobs; complete them before reading it here on the main thread.
        private void ReadFog(EntityManager entityManager)
        {
            _fogView.In(entityManager).TryGetSingleton(out _view);
            var grid = _fogGrid.In(entityManager);
            grid.CompleteDependency();
            grid.TryGetSingleton(out _fog);
        }

        private bool IsHidden(MatchView view, byte owner, float3 position) =>
            BuildingFog.IsHidden(in _view, in _fog, view.Relations, owner, position);
    }
}
