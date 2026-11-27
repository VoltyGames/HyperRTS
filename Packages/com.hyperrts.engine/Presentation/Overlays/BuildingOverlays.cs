using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Capture;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays;

/// <summary>
/// Capture dials and garrison pips over buildings, always on top and the same size at every zoom. Hidden where the
/// building is fogged.
/// </summary>
public sealed class BuildingOverlays(Mesh dialSegment, Mesh disc, Mesh billboard) : IDisposable
{
    private const int DialSteps = 64;
    private const float DialStepDegrees = 360f / DialSteps;
    private const float DialInner = 0.7f;
    private const float BackRim = 1.2f;
    private const float PipPitch = 1.5f;

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

    private readonly InstanceBuffer<OverlayInstance> _dialBacks = new InstanceBuffer<OverlayInstance>(disc);
    private readonly InstanceBuffer<OverlayInstance> _dialSteps = new InstanceBuffer<OverlayInstance>(dialSegment);
    private readonly InstanceBuffer<OverlayInstance> _pipBacks = new InstanceBuffer<OverlayInstance>(billboard);
    private readonly InstanceBuffer<OverlayInstance> _pips = new InstanceBuffer<OverlayInstance>(billboard);
    private readonly EntityExtent _extent = new();
    private LocalFogView _view;
    private FogOfWar _fog;
    private bool _fogRead;
    private Vector3 _cameraRight;
    private Quaternion _cameraRotation;
    private ReferencePixels _pixels;

    public static Mesh DialSegment() => OverlayMeshes.DialSegment(DialSteps, DialInner);

    public void Gather(MatchView view, OverlayStyle style, Camera camera, in ReferencePixels pixels)
    {
        _fogRead = false;
        ReadCamera(camera);
        _pixels = pixels;
        GatherDials(view, style);
        GatherPips(view, style);
        _extent.EndFrame();
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
        _extent.Dispose();
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
            var pixel = _pixels.WorldSize(top);
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
            var pixel = _pixels.WorldSize(top);
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
        _cameraRight = transform.right;
        _cameraRotation = transform.rotation;
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

    private Vector3 Top(EntityManager entityManager, Entity entity, float3 position)
    {
        var radius = EntityRadius.Get(entityManager, entity);
        return new Vector3(position.x, _extent.Top(entityManager, entity, position, radius), position.z);
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
