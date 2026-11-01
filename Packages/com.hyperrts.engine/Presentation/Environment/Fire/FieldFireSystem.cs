using HyperRTS.Simulation.Cues.Surfaces;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Environment.Trampling;
using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Replays;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
{
    /// <summary>
    /// Runs the map's <see cref="FieldFires"/>: lights them from blasts the local player sees on dry ground and burns
    /// them on with the wind, fog or not, so clients agree. Only what the local player sees gets ash and scorch, a fire
    /// effect per cluster and battle haze. Client visuals only; idle without a <see cref="FieldFireSettings"/> or terrain.
    /// </summary>
    // After TrampleStampSystem, which clears the stamps these join.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(TrampleStampSystem))]
    public partial class FieldFireSystem : SystemBase
    {
        // Discs well past a cell, nudged off the grid, so the stamps' soft edges overlap into one char with no tiling.
        private const float AshRadius = 1.3f;
        private const float AshJitter = 0.35f;
        private const float Ash = 1f;
        private const float Scorch = 0.6f;

        private readonly List<FieldFireCluster> _clusters = new();
        private CueVisibility _visibility;
        private EffectPool _pool;
        private BattleHazeSystem _hazeSystem;
        private FieldFireSettings _settings;
        private Terrain _terrain;
        private MapSettings _area;
        private FieldFires _fires;
        private EntityQuery _buildings;
        private uint _buildingPlaces;
        private float _clock;

        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<FieldFireState>();
            _visibility = new CueVisibility(ref CheckedStateRef);
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _streams = new EffectStreams(_pool);
            _buildings = SystemAPI.QueryBuilder().WithAll<BuildingTag, NavObstacle, LocalToWorld>().Build();
            RequireForUpdate<MapSettings>();
            RequireForUpdate<GroundMaterials>();
            RequireForUpdate<TerrainHeight>();
        }

        protected override void OnStopRunning() => Clear();

        protected override void OnUpdate()
        {
            var settings = SystemAPI.TryGetSingleton(out FieldFireSource source) ? source.Settings.Value : null;
            var terrain = Terrain.activeTerrain;
            if (settings == null || terrain == null)
            {
                Clear();
                return;
            }

            CompleteDependency();
            _visibility.Begin();
            Prepare(settings, terrain, SystemAPI.GetSingleton<MapSettings>());
            var now = Clock();
            if (now < _clock)
            {
                _fires.Clear();
                _clusters.Clear();
            }

            var deltaTime = math.max(now - _clock, 0f);
            _clock = now;
            ClearBuildings();
            Ignite();
            // No Wind in the scene means still air.
            SystemAPI.TryGetSingleton(out WindState wind);
            if (_fires.Advance(deltaTime, wind.Push) > 0)
            {
                StampBurntOut();
                Gather();
            }

            Play(deltaTime, SystemAPI.GetSingleton<TerrainHeight>());
        }

        // A new map, settings or terrain starts unburnt.
        private void Prepare(FieldFireSettings settings, Terrain terrain, in MapSettings map)
        {
            if (_fires != null && settings == _settings && terrain == _terrain && _area.SameArea(map))
            {
                return;
            }

            settings.Require();
            (_settings, _terrain, _area) = (settings, terrain, map);
            var size = (int2)math.ceil(map.Size / FieldFires.CellSize);
            var burn = new float2(settings.burnSeconds.x, settings.burnSeconds.y);
            _fires = new FieldFires(map.Min, size, burn, new float2(settings.reach.x, settings.reach.y));
            _fires.MarkDryTerrain(terrain, settings.dryLayers, SystemAPI.GetSingleton<GroundMaterials>());
            _buildingPlaces = 0u;
            _clusters.Clear();
            _clock = Clock();
        }

        // Every building, fogged or not, so all clients clear the same ground. Redone whenever one appears, goes or
        // moves (a new building may show at its spawn point for a frame), before any fire can light the ground.
        private void ClearBuildings()
        {
            var places = _buildings.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var hash = (uint)places.Length + 1u;
            foreach (var place in places)
            {
                hash = math.hash(new int3((int)hash, (int2)math.floor(place.Position.xz)));
            }

            if (hash == _buildingPlaces)
            {
                return;
            }

            _buildingPlaces = hash;
            foreach (var (obstacle, place) in SystemAPI.Query<RefRO<NavObstacle>, RefRO<LocalToWorld>>()
                         .WithAll<BuildingTag>())
            {
                var halfDiagonal = math.length(obstacle.ValueRO.Size) * 0.5f;
                _fires.MarkBare(place.ValueRO.Position.xz, halfDiagonal + _settings.buildingClearance);
            }
        }

        private void Ignite()
        {
            if (!SystemAPI.TryGetSingletonBuffer<CueEvent>(out var cues, true) || cues.IsEmpty)
            {
                return;
            }

            foreach (var cue in cues)
            {
                var seed = Seed(cue);
                if (_settings.Lights(cue, Wetness.Level, seed) && _visibility.IsSeen(cue))
                {
                    _fires.TryIgnite(cue.Position.xz, seed);
                }
            }
        }

        private void StampBurntOut()
        {
            var burntOut = _fires.BurntOut;
            SystemAPI.GetSingletonRW<FieldFireState>().ValueRW.BurntCells += burntOut.Count;
            if (!TrampleStampSystem.MapsOn || burntOut.Count == 0)
            {
                return;
            }

            var stamps = TrampleStampSystem.Find(World);
            if (stamps == null)
            {
                return;
            }

            foreach (var centre in burntOut)
            {
                if (!_visibility.IsSeen(new float3(centre.x, 0f, centre.y)))
                {
                    continue;
                }

                var jitter = new Unity.Mathematics.Random(math.hash(centre) | 1u).NextFloat2(-AshJitter, AshJitter);
                stamps.AddRemains(centre + jitter, AshRadius, new float4(0f, Ash, 0f, 0f));
                stamps.AddScorch(centre + jitter, AshRadius, Scorch);
            }
        }

        /// <summary>Replay time during playback, so pausing holds the fires and seeking back can be told apart.</summary>
        private float Clock() =>
            SystemAPI.TryGetSingleton(out ReplayPlayback replay) ? replay.Time : (float)SystemAPI.Time.ElapsedTime;

        // The impact's own landing spot and source, the same on every client that sees it.
        private static uint Seed(in CueEvent cue) =>
            math.hash(new int4((int2)math.floor(cue.Position.xz * 16f), cue.TypeId, cue.Faction));

        private void Clear()
        {
            _fires = null;
            _settings = null;
            _terrain = null;
            _clusters.Clear();
        }
    }
}
