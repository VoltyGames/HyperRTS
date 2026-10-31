using System;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    public partial class FieldFireSystem
    {
        private const float HazeReach = 10f;
        // A full cluster adds about a fiftieth of a shell's puff each second: a light smudge.
        private const float HazePerCellSecond = 0.0001f;

        // Burning cells at which a cluster's fire plays at full strength.
        private const float FullFireCells = 24f;

        private readonly EffectStreams _streams = new();
        private Camera _camera;
        private float2 _eye;
        private Comparison<FieldFireCluster> _nearer;

        // Nearest the camera first, so the effect cap drops the far fires.
        private void Gather()
        {
            _fires.GatherClusters(_clusters);
            _camera = EffectRange.Main(_camera);
            if (_camera == null)
            {
                return;
            }

            Vector3 eye = _camera.transform.position;
            _eye = new float2(eye.x, eye.z);
            _clusters.Sort(_nearer ??= Nearer);
        }

        private int Nearer(FieldFireCluster a, FieldFireCluster b) =>
            math.distancesq(a.Centre, _eye).CompareTo(math.distancesq(b.Centre, _eye));

        // A cluster in fog skips its stream, which then starts afresh once seen again.
        private void Play(float deltaTime, in TerrainHeight terrain)
        {
            var haze = (_hazeSystem ??= World.GetExistingSystemManaged<BattleHazeSystem>())?.Haze;
            _streams.BeginFrame();
            var playing = 0;
            foreach (var cluster in _clusters)
            {
                var position = new float3(cluster.Centre.x, terrain.Height(cluster.Centre), cluster.Centre.y);
                if (!_visibility.IsSeen(position))
                {
                    continue;
                }

                haze?.Add(cluster.Centre, HazeReach, HazePerCellSecond * cluster.Cells * deltaTime);
                if (playing++ >= _settings.maxEffects)
                {
                    continue;
                }

                var state = _pool.Step(_streams, _settings.rig, Entity.Null, cluster.Key, position);
                state?.Emit(state.Rig.LoopRotation(Quaternion.identity, state.Key), deltaTime,
                    math.saturate(cluster.Cells / FullFireCells), state.Rig.LoopScale(state.Key));
            }

            _streams.EndFrame();
        }
    }
}
