using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Flies and draws the <see cref="AmbientLife"/> bird flocks, scattering them from this frame's impacts. Client
    /// visuals only; idle without an AmbientLife, with no flocks, or on Low effect detail.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BirdFlockSystem : SystemBase
    {
        private readonly List<BirdFlock> _flocks = new();
        private float3[] _impacts = new float3[16];
        private BirdRenderer _renderer;
        private AmbientLife _life;
        private BlobAssetReference<HeightfieldBlob> _terrain;
        private Camera _camera;

        protected override void OnCreate()
        {
            _renderer = new BirdRenderer();
            RequireForUpdate<TerrainHeight>();
            RequireForUpdate<CueQueue>();
        }

        protected override void OnDestroy() => _renderer.Dispose();

        protected override void OnUpdate()
        {
            var life = AmbientLife.Active;
            var off = life == null || life.flocks == 0;
            if (off || EffectDetail.Level == DetailLevel.Low)
            {
                return;
            }

            CompleteDependency();
            var terrain = SystemAPI.GetSingleton<TerrainHeight>();
            if (life != _life || terrain.Blob != _terrain)
            {
                Populate(life, terrain);
            }

            var impacts = Impacts();
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var flock in _flocks)
            {
                flock.Step(deltaTime, impacts);
            }

            _camera = EffectRange.Main(_camera);
            if (_camera != null)
            {
                _renderer.Draw(_flocks, life.birdMaterial, life.birdSize, _camera.transform.position);
            }
        }

        /// <summary>New flocks for a new AmbientLife or map; the old ones' heightfield may be gone.</summary>
        private void Populate(AmbientLife life, in TerrainHeight terrain)
        {
            life.RequireBirds();
            _life = life;
            _terrain = terrain.Blob;
            _flocks.Clear();
            var area = new SkyArea(terrain);
            var settings = life.Birds;
            var random = new Unity.Mathematics.Random(0x9E3779B9u);
            for (var i = 0; i < life.flocks; i++)
            {
                var count = random.NextInt(life.birdsPerFlock.x, life.birdsPerFlock.y + 1);
                _flocks.Add(new BirdFlock(count, settings, area, random.NextUInt()));
            }
        }

        private ReadOnlySpan<float3> Impacts()
        {
            var queue = SystemAPI.GetSingletonBuffer<CueEvent>(true);
            if (_impacts.Length < queue.Length)
            {
                _impacts = new float3[math.ceilpow2(queue.Length)];
            }

            var count = 0;
            foreach (var cue in queue)
            {
                if (cue.Slot == CueSlot.Impact)
                {
                    _impacts[count++] = cue.Position;
                }
            }

            return new ReadOnlySpan<float3>(_impacts, 0, count);
        }
    }
}
