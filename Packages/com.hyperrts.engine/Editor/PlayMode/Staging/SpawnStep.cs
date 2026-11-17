using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.PlayMode.Cheats;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Spawns a row of one prefab for an owner under a group label.</summary>
    internal sealed class SpawnStep : StagingStep
    {
        private readonly string _group;
        private readonly string _prefab;
        private readonly byte _owner;
        private readonly int _count;
        private readonly float2 _point;
        private readonly float _spacing;
        private readonly float _yaw;
        private readonly SurfaceMaterial? _ground;
        private readonly float _radius;

        private SpawnStep(StagingObject json)
        {
            _group = json.Text("as");
            _prefab = json.Text("prefab");
            _owner = (byte)Math.Clamp(json.Whole("owner"), 0, byte.MaxValue);
            _count = json.Whole("count");
            _point = json.Point("at");
            _spacing = json.Has("spacing") ? json.Number("spacing") : 0f;
            _yaw = json.Number("yaw");
            if (_count < 1)
            {
                throw json.Problem($"needs a 'count' of at least 1, not {_count}");
            }

            if (_count > 1 && _spacing <= 0f)
            {
                throw json.Problem("needs a 'spacing' above 0 for more than one unit");
            }

            if (_group == SampleStep.CameraKey)
            {
                throw json.Problem($"cannot name its group '{SampleStep.CameraKey}'; sample uses that for the camera");
            }

            if (json.Has("ground"))
            {
                _ground = ReadGround(json);
                _radius = json.Number("radius");
            }
        }

        public override string Kind => "spawn";

        public override string Defines => _group;

        public override bool EndsRunOnError => true;

        public static StagingStep Read(StagingObject step) => new SpawnStep(step.Child("spawn"));

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            var prefab = FindPrefab(entityManager, _prefab);
            var centre = _ground.HasValue
                ? new GroundSearch(entityManager).Nearest(_point, _ground.Value, _radius)
                : _point;
            var row = new SpawnRow(centre, _yaw, _spacing, _count);
            var units = new List<Entity>();
            for (var i = 0; i < _count; i++)
            {
                var unit = SpawnCheat.SpawnAt(entityManager, prefab, _owner, row.Position(i), math.radians(_yaw));
                if (unit != Entity.Null)
                {
                    units.Add(unit);
                }
            }

            run.AddGroup(new StagingGroup(_group, _owner, units));
            if (units.Count < _count)
            {
                throw new InvalidOperationException($"{_count - units.Count} of {_count} {_prefab} not spawned: the ground there is blocked");
            }
        }

        private static SurfaceMaterial ReadGround(StagingObject json)
        {
            var name = json.Text("ground");
            if (Enum.TryParse<SurfaceMaterial>(name, true, out var material) && Enum.IsDefined(typeof(SurfaceMaterial), material))
            {
                return material;
            }

            throw json.Problem($"has no ground material '{name}'; known: {string.Join(", ", Enum.GetNames(typeof(SurfaceMaterial)))}");
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
                || name.Contains(known, StringComparison.OrdinalIgnoreCase)).OrderBy(known => known).Take(8);
            var hint = near.Any() ? $"near matches: {string.Join(", ", near)}" : $"{names.Count} prefabs, none alike";
            throw new InvalidOperationException($"no spawnable prefab named '{name}' ({hint})");
        }
    }
}
