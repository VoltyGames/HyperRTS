using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Gathers this frame's stamps from vehicles, building footprints and wakes the local player sees.</summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class InteractionStampSystem : SystemBase
    {
        private Dictionary<Entity, float2> _last = new();
        private Dictionary<Entity, float2> _current = new();
        private Dictionary<Entity, float2> _lastAfloat = new();
        private Dictionary<Entity, float2> _currentAfloat = new();
        private EntityQuery _vehicles;
        private EntityQuery _buildings;
        private EntityQuery _units;

        public InteractionStamps Stamps { get; } = new();

        protected override void OnCreate()
        {
            _vehicles = SystemAPI.QueryBuilder().WithAll<TrackMarks, LocalToWorld>()
                .WithNone<FogHidden, DisableRendering>().Build();
            _buildings = SystemAPI.QueryBuilder().WithAll<BuildingTag, NavObstacle, LocalToWorld>()
                .WithNone<FogHidden, DisableRendering>().Build();
            _units = SystemAPI.QueryBuilder().WithAll<NavAgent, LocalToWorld>()
                .WithNone<FogHidden, DisableRendering>().Build();
        }

        protected override void OnUpdate()
        {
            Stamps.Clear();
            if (InteractionMap.Quality == InteractionQuality.Off)
            {
                _last.Clear();
                _lastAfloat.Clear();
                return;
            }

            CompleteDependency();
            AddVehicles();
            AddBuildings();
            AddWakes();
        }

        private void AddVehicles()
        {
            // No terrain is a flat world at height 0 (TerrainHeight's default).
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            using var entities = _vehicles.ToEntityArray(Allocator.Temp);
            using var marks = _vehicles.ToComponentDataArray<TrackMarks>(Allocator.Temp);
            using var transforms = _vehicles.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            _current.Clear();
            for (var i = 0; i < entities.Length; i++)
            {
                var position = transforms[i].Position;
                var forward = transforms[i].Forward.xz;
                if (!OnGround(terrain, position) || math.lengthsq(forward) < 1e-4f)
                {
                    continue;
                }

                forward = math.normalize(forward);
                _current[entities[i]] = position.xz;
                var advance = Advance(entities[i], position.xz, forward);
                InteractionMath.AddVehicle(marks[i], position.xz, forward, advance, Stamps.Trample, Stamps.Tracks);
            }

            (_last, _current) = (_current, _last);
        }

        private static bool OnGround(in TerrainHeight terrain, float3 position) =>
            position.y - terrain.Height(position.xz) <= InteractionMath.MaxGroundGap;

        /// <summary>Metres driven along <paramref name="forward"/> since last frame; 0 when new in sight or teleported.</summary>
        private float Advance(Entity vehicle, float2 position, float2 forward)
        {
            if (!_last.TryGetValue(vehicle, out var last))
            {
                return 0f;
            }

            var step = position - last;
            return math.length(step) > InteractionMath.MaxStep ? 0f : math.dot(step, forward);
        }

        // Stamped every frame: max blending is idempotent and holds grass down.
        private void AddBuildings()
        {
            using var footprints = _buildings.ToComponentDataArray<NavObstacle>(Allocator.Temp);
            using var transforms = _buildings.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < footprints.Length; i++)
            {
                Stamps.Trample.Add(InteractionMath.Footprint(transforms[i].Position.xz, footprints[i].Size));
            }
        }

        private void AddWakes()
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (!SystemAPI.TryGetSingleton(out NavGrid grid) || !grid.IsCreated || deltaTime <= 0f)
            {
                _lastAfloat.Clear();
                return;
            }

            _currentAfloat.Clear();

            using var entities = _units.ToEntityArray(Allocator.Temp);
            using var agents = _units.ToComponentDataArray<NavAgent>(Allocator.Temp);
            using var transforms = _units.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var position = transforms[i].Position;
                if (!InWater(grid, position))
                {
                    continue;
                }

                _currentAfloat[entities[i]] = position.xz;
                var step = _lastAfloat.TryGetValue(entities[i], out var last) ? position.xz - last : float2.zero;
                var travelled = math.length(step);
                var speed = travelled / deltaTime;
                if (speed >= InteractionMath.MinWakeSpeed && travelled <= InteractionMath.MaxStep)
                {
                    Stamps.AddWake(position.xz, step / travelled, speed, agents[i].Radius);
                }
            }

            (_lastAfloat, _currentAfloat) = (_currentAfloat, _lastAfloat);
        }

        // The height checks leave out aircraft and units on bridges.
        private static bool InWater(in NavGrid grid, float3 position)
        {
            var water = (grid.Surface(grid.WorldToCell(position)) & NavSurface.Water) != 0;
            return water && grid.Terrain.Height(position.xz) < grid.WaterLevel &&
                   position.y <= grid.WaterLevel + InteractionMath.MaxGroundGap;
        }
    }
}
