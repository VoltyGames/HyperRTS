using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Effects;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Forms <see cref="AmbientLife"/> dust devils on allowed ground near the camera's focus and streams their rig as
    /// they drift downwind. Client visuals only; idle without an AmbientLife, with none allowed, or on Low effect detail.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DustDevilSystem : SystemBase
    {
        private const float Reach = 120f;

        private readonly List<DustDevil> _devils = new();
        private EffectPool _pool;
        private Camera _camera;
        private AmbientLife _life;
        private Random _random = new(0x2545F491u);
        private float _untilSpawn;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<TerrainHeight>();
            RequireForUpdate<GroundMaterials>();
        }

        protected override void OnUpdate()
        {
            var life = AmbientLife.Active;
            var off = life == null || life.maxDustDevils == 0;
            if (off || EffectDetail.Level == EffectDetailLevel.Low)
            {
                _devils.Clear();
                return;
            }

            if (life != _life)
            {
                Begin(life);
            }

            _camera = EffectRange.Main(_camera);
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
            if (!DustDevilSites.TryPick(ground, focus.xz, Reach, _life.dustDevilGround, ref _random, out var site))
            {
                return;
            }

            var lifetime = _random.NextFloat(_life.dustDevilLifetime.x, _life.dustDevilLifetime.y);
            var position = new float3(site.x, terrain.Height(site), site.y);
            _devils.Add(new DustDevil(position, lifetime, _random.NextFloat(2f * math.PI)));
        }

        private void Advance(float deltaTime, in TerrainHeight terrain)
        {
            var rig = _pool.Get(_life.dustDevilRig);
            Vector3 wind = Wind.Downwind;
            var downwind = math.normalizesafe(new float2(wind.x, wind.z));
            for (var i = _devils.Count - 1; i >= 0; i--)
            {
                var devil = _devils[i];
                Vector3 from = devil.Step(deltaTime, downwind, _life.dustDevilSpeed, terrain);
                if (devil.Done)
                {
                    _devils.RemoveAt(i);
                    continue;
                }

                if (rig == null || !EffectRange.InRange(_camera, rig, from))
                {
                    continue;
                }

                rig.Stream(from, devil.Position, Quaternion.identity, deltaTime, devil.Strength,
                    EffectDetail.Density, ref devil.Carry);
            }
        }

        private float NextInterval() => _random.NextFloat(_life.dustDevilInterval.x, _life.dustDevilInterval.y);
    }
}
