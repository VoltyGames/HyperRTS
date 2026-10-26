using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Animation;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Gathers this frame's stamps from vehicles, infantry footsteps, building footprints, buried remains and wakes the
    /// local player sees. Footsteps need the full-resolution maps.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class InteractionStampSystem : SystemBase
    {
        private readonly FramePositions _grounded = new();
        private readonly FramePositions _afloat = new();
        private readonly FramePositions _walking = new();
        private EntityQuery _vehicles;
        private EntityQuery _squads;
        private EntityQuery _buildings;
        private EntityQuery _remains;

        public InteractionStamps Stamps { get; } = new();

        protected override void OnCreate()
        {
            _vehicles = SystemAPI.QueryBuilder().WithAll<TrackMarks, LocalToWorld>()
                .WithNone<FogHidden, Inside>().Build();
            _squads = SystemAPI.QueryBuilder().WithAll<SquadFigures, SquadFigure, LocalToWorld>()
                .WithNone<FogHidden, Inside>().Build();
            _buildings = SystemAPI.QueryBuilder().WithAll<BuildingTag, NavObstacle, LocalToWorld>()
                .WithNone<FogHidden, Inside>().Build();
            _remains = SystemAPI.QueryBuilder().WithAll<Decay, NavObstacle, LocalToWorld>()
                .WithNone<FogHidden, Inside>().Build();
        }

        protected override void OnUpdate()
        {
            Stamps.Clear();
            if (InteractionMap.Quality == InteractionQuality.Off)
            {
                _grounded.Forget();
                _afloat.Forget();
                _walking.Forget();
                return;
            }

            CompleteDependency();
            AddVehicles();
            AddFootsteps();
            AddBuildings();
            AddBuriedRemains();
            AddWakes();
        }

        private void AddVehicles()
        {
            // No terrain is a flat world at height 0 (TerrainHeight's default).
            SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            using var entities = _vehicles.ToEntityArray(Allocator.Temp);
            using var marks = _vehicles.ToComponentDataArray<TrackMarks>(Allocator.Temp);
            using var transforms = _vehicles.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var position = transforms[i].Position;
                var forward = transforms[i].Forward.xz;
                if (!OnGround(terrain, position) || math.lengthsq(forward) < 1e-4f)
                {
                    continue;
                }

                forward = math.normalize(forward);
                _grounded.Record(entities[i], position.xz);
                var advance = Advance(entities[i], position.xz, forward);
                InteractionMath.AddVehicle(marks[i], position.xz, forward, advance, Stamps.Trample, Stamps.Tracks);
            }

            _grounded.EndFrame();
        }

        private void AddFootsteps()
        {
            if (InteractionMap.Quality != InteractionQuality.Full)
            {
                _walking.Forget();
                return;
            }

            using var entities = _squads.ToEntityArray(Allocator.Temp);
            foreach (var squad in entities)
            {
                var position = SystemAPI.GetComponent<LocalToWorld>(squad).Position.xz;
                _walking.Record(squad, position);
                var step = _walking.TryGetLast(squad, out var last) ? position - last : float2.zero;
                var stride = math.length(step);
                if (stride < 1e-3f || stride > InteractionMath.MaxStep)
                {
                    continue;
                }

                foreach (var figure in SystemAPI.GetBuffer<SquadFigure>(squad))
                {
                    var walking = figure.State is FigureState.Walk or FigureState.Run;
                    if (walking)
                    {
                        var feet = SystemAPI.GetComponent<LocalToWorld>(figure.Figure).Position.xz;
                        Stamps.Trample.Add(InteractionMath.Footsteps(feet, step / stride, stride));
                    }
                }
            }

            _walking.EndFrame();
        }

        private static bool OnGround(in TerrainHeight terrain, float3 position) =>
            position.y - terrain.Height(position.xz) <= InteractionMath.MaxGroundGap;

        /// <summary>Metres driven along <paramref name="forward"/> since last frame; 0 when new in sight or teleported.</summary>
        private float Advance(Entity vehicle, float2 position, float2 forward)
        {
            if (!_grounded.TryGetLast(vehicle, out var last))
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

        // The scar deepens as the remains are buried, so the plot keeps a burnt mark of what stood there.
        private void AddBuriedRemains()
        {
            using var decays = _remains.ToComponentDataArray<Decay>(Allocator.Temp);
            using var footprints = _remains.ToComponentDataArray<NavObstacle>(Allocator.Temp);
            using var transforms = _remains.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < decays.Length; i++)
            {
                var forward = transforms[i].Forward.xz;
                var buried = decays[i].DespawnProgress > 0f;
                if (buried && math.lengthsq(forward) > 1e-4f)
                {
                    Stamps.AddFootprintScar(transforms[i].Position.xz, math.normalize(forward), footprints[i].Size,
                        decays[i].DespawnProgress);
                }
            }
        }

        private void AddWakes()
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (!SystemAPI.TryGetSingleton(out NavGrid grid) || !grid.IsCreated || deltaTime <= 0f)
            {
                _afloat.Forget();
                return;
            }

            foreach (var (agent, transform, entity) in SystemAPI.Query<RefRO<NavAgent>, RefRO<LocalToWorld>>()
                         .WithNone<FogHidden, Inside>().WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                if (!InWater(grid, position))
                {
                    continue;
                }

                _afloat.Record(entity, position.xz);
                var step = _afloat.TryGetLast(entity, out var last) ? position.xz - last : float2.zero;
                var travelled = math.length(step);
                var speed = travelled / deltaTime;
                if (speed >= InteractionMath.MinWakeSpeed && travelled <= InteractionMath.MaxStep)
                {
                    Stamps.AddWake(position.xz, step / travelled, speed, agent.ValueRO.Radius);
                }
            }

            _afloat.EndFrame();
        }

        // The height checks leave out aircraft and units on bridges.
        private static bool InWater(in NavGrid grid, float3 position)
        {
            var water = (grid.Surface(grid.WorldToCell(position)) & NavSurface.Water) != 0;
            return water && grid.Terrain.Height(position.xz) < grid.WaterLevel &&
                   position.y <= grid.WaterLevel + InteractionMath.MaxGroundGap;
        }

        /// <summary>Last frame's positions, kept to measure how far each unit moved.</summary>
        private sealed class FramePositions
        {
            private Dictionary<Entity, float2> _last = new();
            private Dictionary<Entity, float2> _current = new();

            public void Forget() => _last.Clear();

            public bool TryGetLast(Entity entity, out float2 position) => _last.TryGetValue(entity, out position);

            public void Record(Entity entity, float2 position) => _current[entity] = position;

            /// <summary>This frame's positions become last frame's, so units out of sight are forgotten.</summary>
            public void EndFrame()
            {
                (_last, _current) = (_current, _last);
                _current.Clear();
            }
        }
    }
}
