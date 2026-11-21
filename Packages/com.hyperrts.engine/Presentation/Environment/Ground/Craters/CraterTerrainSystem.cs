using System.Collections.Generic;
using HyperRTS.Presentation.Environment.Ground.Stamps;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Craters
{
    /// <summary>
    /// Dents the active terrain under stamped crater bowls and trenches, raises it under their rims and levels it under finished buildings, and restores it on a replay seek or map change.
    /// Visual only: the simulation's ground never changes.
    /// </summary>
    // Before TrampleStampSystem clears last frame's stamps, which TrampleMap has drawn by now.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(TrampleStampSystem))]
    public partial class CraterTerrainSystem : SystemBase
    {
        public const float SyncSeconds = 0.5f;

        private CraterTerrain _crater;
        private readonly BuildingLevels _levels = new();
        private readonly Dictionary<Entity, BuildingLevel> _wanted = new();
        private Texture _tracks;
        private float _syncedAt = float.NegativeInfinity;

        protected override void OnDestroy() => Release();

        protected override void OnUpdate()
        {
            var stamps = TrampleStampSystem.Find(World);
            if (stamps == null || !Bind())
            {
                Release();
                return;
            }

            if (stamps.Reset)
            {
                _crater.Restore();
                _levels.Clear();
            }

            var min = new float2(float.MaxValue);
            var max = new float2(float.MinValue);
            AddBowls(stamps, ref min, ref max);
            _levels.TakeFilled(ref min, ref max);
            if (_levels.Replace(WantedLevels(), stamps, ref min, ref max))
            {
                _crater.SetBuildings(_levels.All);
            }

            var landed = math.all(min <= max);
            if (landed)
            {
                _crater.Dent(min, max);
            }

            var due = UnityEngine.Time.unscaledTime - _syncedAt >= SyncSeconds;
            if (_crater.Unsynced && due && !landed)
            {
                _crater.Sync();
                _syncedAt = UnityEngine.Time.unscaledTime;
            }
        }

        /// <summary>False without a terrain or trample maps. New maps start clear, so the terrain does too.</summary>
        private bool Bind()
        {
            var terrain = Terrain.activeTerrain;
            var tracks = Shader.GetGlobalTexture(TrampleSurfaces.TracksGlobal.TextureId);
            if (terrain == null || tracks is not RenderTexture)
            {
                return false;
            }

            if (_crater == null || _crater.Terrain != terrain || _tracks != tracks)
            {
                Release();
                _crater = new CraterTerrain(terrain);
                _tracks = tracks;
            }

            return true;
        }

        // Bowls, trenches and rims move the ground; a pure scorch does not.
        private static void AddBowls(TrampleStamps stamps, ref float2 min, ref float2 max)
        {
            foreach (var scar in stamps.Scars)
            {
                var shaped = scar.Value.x > 0f || scar.Rim > 0f;
                if (shaped)
                {
                    min = math.min(min, scar.Center - scar.Extent);
                    max = math.max(max, scar.Center + scar.Extent);
                }
            }
        }

        // Every finished, seen building, neutral ones too. A hidden one stays unlevelled so the ground never gives it away.
        private Dictionary<Entity, BuildingLevel> WantedLevels()
        {
            _wanted.Clear();
            foreach (var (obstacle, place, entity) in SystemAPI.Query<RefRO<NavObstacle>, RefRO<LocalToWorld>>()
                         .WithAll<BuildingTag>().WithNone<ConstructionProgress, FogHidden>().WithEntityAccess())
            {
                if (obstacle.ValueRO.Open || IsBridge(entity))
                {
                    continue;
                }

                var area = TurnedRect.Create(place.ValueRO.Value, place.ValueRO.Position.xz, obstacle.ValueRO.Size);
                _wanted[entity] = new BuildingLevel(area, place.ValueRO.Position.y);
            }

            return _wanted;
        }

        // A bridge spans the ground under it, which the map shaped for it; levelling to its deck would bury the river.
        private bool IsBridge(Entity entity) =>
            SystemAPI.HasComponent<NavArea>(entity) && SystemAPI.GetComponent<NavArea>(entity).Kind == NavAreaKind.Deck;

        private void Release()
        {
            _crater?.Dispose();
            _crater = null;
            _tracks = null;
            _levels.Clear();
        }
    }
}
