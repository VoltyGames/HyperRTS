using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.PlayMode.Cheats;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Surfaces;
using Newtonsoft.Json;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Spawns a row of one prefab for an owner under a group label.</summary>
internal sealed class SpawnStep : StagingStep
{
    private const float SearchStep = 8f;

    [JsonProperty("as")]
    public string group;
    public string prefab;
    public int owner;
    public int count;
    public float[] at;
    public float? spacing;
    public float yaw;
    public string ground;
    public float? radius;

    private float2 _point;
    private SurfaceMaterial? _ground;

    public override string Defines => group;

    public override bool EndsRunOnError => true;

    public override void Check()
    {
        Need(group, "as");
        Need(prefab, "prefab");
        if (owner is < 0 or > byte.MaxValue)
        {
            throw new InvalidOperationException($"has an 'owner' of {owner}, not 0 to 255");
        }

        _point = Point(at, "at");
        if (count < 1)
        {
            throw new InvalidOperationException($"needs a 'count' of at least 1, not {count}");
        }

        if (count > 1 && (spacing ?? 0f) <= 0f)
        {
            throw new InvalidOperationException("needs a 'spacing' above 0 for more than one unit");
        }

        if (group == SampleStep.CameraKey)
        {
            throw new InvalidOperationException($"cannot name its group '{SampleStep.CameraKey}'; sample uses that for the camera");
        }

        if (ground == null)
        {
            return;
        }

        _ground = ReadGround();
        if (radius == null)
        {
            throw new InvalidOperationException("needs a 'radius' with 'ground'");
        }
    }

    public override void Run(StagingRun run)
    {
        var entityManager = run.World;
        var template = FindPrefab(entityManager, prefab);
        var centre = _ground.HasValue
            ? new GroundSearch(entityManager).Nearest(_point, _ground.Value, radius ?? 0f)
            : _point;
        var row = new SpawnRow(centre, yaw, spacing ?? 0f, count);
        var units = new List<Entity>();
        for (var i = 0; i < count; i++)
        {
            var unit = SpawnNear(entityManager, template, row.Position(i));
            if (unit != Entity.Null)
            {
                units.Add(unit);
            }
        }

        run.AddGroup(new StagingGroup(group, (byte)owner, units));
        if (units.Count < count)
        {
            throw new InvalidOperationException($"{count - units.Count} of {count} {prefab} not spawned: the ground there is blocked");
        }
    }

    // A building on blocked ground takes the nearest free spot within the radius, tried in widening squares.
    private Entity SpawnNear(EntityManager entityManager, Entity template, float2 spot)
    {
        var facing = math.radians(yaw);
        var unit = SpawnCheat.SpawnAt(entityManager, template, (byte)owner, spot, facing);
        for (var ring = 1; unit == Entity.Null && ring * SearchStep <= (radius ?? 0f); ring++)
        {
            for (var x = -ring; x <= ring && unit == Entity.Null; x++)
            {
                for (var z = -ring; z <= ring && unit == Entity.Null; z++)
                {
                    if (math.max(math.abs(x), math.abs(z)) == ring)
                    {
                        unit = SpawnCheat.SpawnAt(entityManager, template, (byte)owner, spot + new float2(x, z) * SearchStep, facing);
                    }
                }
            }
        }

        return unit;
    }

    private SurfaceMaterial ReadGround()
    {
        if (Enum.TryParse<SurfaceMaterial>(ground, true, out var material) && Enum.IsDefined(typeof(SurfaceMaterial), material))
        {
            return material;
        }

        throw new InvalidOperationException($"has no ground material '{ground}'; known: {string.Join(", ", Enum.GetNames(typeof(SurfaceMaterial)))}");
    }

    private static Entity FindPrefab(EntityManager entityManager, string name)
    {
        var names = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
        foreach (var prefab in SpawnCheat.Prefabs(entityManager))
        {
            names[entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString()] = prefab;
        }

        if (names.TryGetValue(name, out var found))
        {
            return found;
        }

        var near = names.Keys.Where(known => known.Contains(name, StringComparison.OrdinalIgnoreCase)
            || name.Contains(known, StringComparison.OrdinalIgnoreCase)).Order().Take(8);
        var hint = near.Any() ? $"near matches: {string.Join(", ", near)}" : $"{names.Count} prefabs, none alike";
        throw new InvalidOperationException($"no spawnable prefab named '{name}' ({hint})");
    }
}
