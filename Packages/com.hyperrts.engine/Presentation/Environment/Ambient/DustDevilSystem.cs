using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Cues.Surfaces;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Forms <see cref="AmbientLife"/> dust devils on allowed ground near the camera and drifts them downwind. Client visuals only.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DustDevilSystem : SystemBase
    {
        private const float Reach = 120f;

        private readonly List<DustDevil> _devils = new();
        private EffectStreams _streams;
        private EffectPool _pool;
        private Camera _camera;
        private AmbientLife _life;
        private Random _random = new(0x2545F491u);
        private float _untilSpawn;
        private int _nextId;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _streams = new EffectStreams(_pool);
            RequireForUpdate<TerrainHeight>();
            RequireForUpdate<GroundMaterials>();
        }

        protected override void OnUpdate()
        {
            var life = SystemAPI.TryGetSingleton(out AmbientLifeSource source) ? source.Life.Value : null;
            var off = life == null || life.maxDustDevils == 0;
            if (off || PresentationQuality.Current.Effects == DetailLevel.Low)
            {
                _devils.Clear();
                return;
            }

            if (life != _life)
            {
                Begin(life);
            }

            _camera = MainCamera.Find(_camera);
            if (_camera == null)
            {
                return;
            }

            CompleteDependency();
            var terrain = SystemAPI.GetSingleton<TerrainHeight>();
            var deltaTime = SystemAPI.Time.DeltaTime;
            TrySpawn(deltaTime, terrain);
            Advance(deltaTime, terrain);
        }

        private void Begin(AmbientLife life)
        {
            life.RequireDustDevils();
            _life = life;
            _devils.Clear();
            _untilSpawn = NextInterval();
        }

        private void TrySpawn(float deltaTime, in TerrainHeight terrain)
        {
            _untilSpawn -= deltaTime;
            if (_untilSpawn > 0f)
            {
                return;
            }

            _untilSpawn = NextInterval();
            if (_devils.Count >= _life.maxDustDevils)
            {
                return;
            }

            var eye = _camera.transform;
            var focus = ViewGround.Focus(terrain, eye.position, eye.forward, _camera.farClipPlane);
            var ground = SystemAPI.GetSingleton<GroundMaterials>();
            if (!DustDevilSites.TryPick(ground, focus.xz, Reach, _life.dustDevilGround,
                    ref _random, out var site))
            {
                return;
            }

            var lifetime = _random.NextFloat(_life.dustDevilLifetime.x, _life.dustDevilLifetime.y);
            var position = new float3(site.x, terrain.Height(site), site.y);
            _devils.Add(new DustDevil(_nextId++, position, lifetime, _random.NextFloat(2f * math.PI)));
        }

        private void Advance(float deltaTime, in TerrainHeight terrain)
        {
            // No Wind in the scene means still air.
            SystemAPI.TryGetSingleton(out WindState wind);
            var downwind = math.normalizesafe(wind.Downwind.xz);
            _streams.BeginFrame();
            for (var i = _devils.Count - 1; i >= 0; i--)
            {
                var devil = _devils[i];
                devil.Step(deltaTime, downwind, _life.dustDevilSpeed, terrain);
                if (devil.Done)
                {
                    _devils.RemoveAt(i);
                    continue;
                }

                var state = _streams.Step(_life.dustDevilRig, Entity.Null, devil.Id, devil.Position);
                state?.Emit(Quaternion.identity, deltaTime, devil.Strength);
            }

            _streams.EndFrame();
        }

        private float NextInterval() => _random.NextFloat(_life.dustDevilInterval.x, _life.dustDevilInterval.y);
    }
}
