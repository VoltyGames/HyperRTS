using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Environment.Weather;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Fire
{
    /// <summary>Plays the flames and battle haze of the burning clusters the local player sees.</summary>
    public sealed class FieldFireEffects
    {
        private const float HazeReach = 10f;
        // A full cluster adds about a fiftieth of a shell's puff each second: a light smudge.
        private const float HazePerCellSecond = 0.0001f;

        // Burning cells at which a cluster's fire plays at full strength.
        private const float FullFireCells = 24f;

        private readonly EffectStreams _streams;
        private Camera _camera;
        private float2 _eye;
        private Comparison<FieldFireCluster> _nearer;

        public FieldFireEffects(EffectPool pool) => _streams = new EffectStreams(pool);

        // Nearest the camera first, so the effect cap drops the far fires.
        public void SortNearest(List<FieldFireCluster> clusters)
        {
            _camera = MainCamera.Find(_camera);
            if (_camera == null)
            {
                return;
            }

            Vector3 eye = _camera.transform.position;
            _eye = new float2(eye.x, eye.z);
            clusters.Sort(_nearer ??= Nearer);
        }

        private int Nearer(FieldFireCluster a, FieldFireCluster b) =>
            math.distancesq(a.Centre, _eye).CompareTo(math.distancesq(b.Centre, _eye));

        // A cluster in fog skips its stream, which then starts afresh once seen again.
        public void Play(List<FieldFireCluster> clusters, FieldFireSettings settings, ref CueVisibility visibility,
            in TerrainHeight terrain, BattleHaze haze, float deltaTime)
        {
            _streams.BeginFrame();
            var playing = 0;
            foreach (var cluster in clusters)
            {
                var position = new float3(cluster.Centre.x, terrain.Height(cluster.Centre), cluster.Centre.y);
                if (!visibility.IsSeen(position))
                {
                    continue;
                }

                haze?.Add(cluster.Centre, HazeReach, HazePerCellSecond * cluster.Cells * deltaTime);
                if (playing++ >= settings.maxEffects)
                {
                    continue;
                }

                var state = _streams.Step(settings.rig, Entity.Null, cluster.Key, position);
                state?.Emit(state.Rig.LoopRotation(Quaternion.identity, state.Key), deltaTime,
                    math.saturate(cluster.Cells / FullFireCells), state.Rig.LoopScale(state.Key));
            }

            _streams.EndFrame();
        }
    }
}
