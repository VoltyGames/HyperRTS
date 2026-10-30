using HyperRTS.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Dents the active terrain under the crater bowls stamped last frame, reading the heights back at most every
    /// <see cref="SyncSeconds"/> on a frame with no new crater. Restores the terrain on a replay seek, when the maps
    /// go or change, and when the terrain or this world goes. Visual only: the simulation's ground never changes.
    /// </summary>
    // Before InteractionStampSystem clears last frame's stamps, which InteractionMap has drawn by now.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(InteractionStampSystem))]
    public partial class CraterTerrainSystem : SystemBase
    {
        public const float SyncSeconds = 0.5f;

        private Shader _shader;
        private InteractionStampSystem _stampSystem;
        private CraterTerrain _crater;
        private Texture _tracks;
        private float _syncedAt = float.NegativeInfinity;

        protected override void OnCreate() => _shader = Resources.Load<Shader>(CraterTerrain.ShaderPath);

        protected override void OnDestroy() => Release();

        protected override void OnUpdate()
        {
            var stamps = (_stampSystem ??= World.GetExistingSystemManaged<InteractionStampSystem>())?.Stamps;
            if (stamps == null || !Bind())
            {
                Release();
                return;
            }

            if (stamps.Reset)
            {
                _crater.Restore();
            }

            var landed = TryGetBowls(stamps, out var min, out var max);
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

        /// <summary>False without a terrain or interaction maps. New maps start clear, so the terrain does too.</summary>
        private bool Bind()
        {
            var terrain = Terrain.activeTerrain;
            var tracks = Shader.GetGlobalTexture(InteractionSurfaces.TracksId);
            if (terrain == null || tracks is not RenderTexture)
            {
                return false;
            }

            if (_crater == null || _crater.Terrain != terrain || _tracks != tracks)
            {
                Release();
                _crater = new CraterTerrain(terrain, _shader);
                _tracks = tracks;
            }

            return true;
        }

        private static bool TryGetBowls(InteractionStamps stamps, out float2 min, out float2 max)
        {
            min = new float2(float.MaxValue);
            max = new float2(float.MinValue);
            foreach (var scar in stamps.Scars)
            {
                var bowl = scar.Value.x > 0f;
                if (bowl)
                {
                    min = math.min(min, scar.Center - scar.HalfSize);
                    max = math.max(max, scar.Center + scar.HalfSize);
                }
            }

            return math.all(min <= max);
        }

        private void Release()
        {
            _crater?.Dispose();
            _crater = null;
            _tracks = null;
        }
    }
}
