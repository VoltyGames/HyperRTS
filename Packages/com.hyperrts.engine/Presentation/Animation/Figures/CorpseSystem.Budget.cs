using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class CorpseSystem
    {
        /// <summary>Seconds a figure of a gone squad waits for its corpse before it hides with its squad.</summary>
        public const float FallWait = 0.1f;

        private static readonly ProfilerMarker MakeMarker = new("CorpseSystem.Make");

        /// <summary>A figure of a gone squad waiting for the frame's budget to copy it as a corpse.</summary>
        private struct WaitingFall : IComparable<WaitingFall>
        {
            public ShownFigure Shown;
            public Entity Prefab;
            public SquadFigure Baked;
            public float DieHold;
            public byte Owner;
            public float Since;
            public float Distance;

            public readonly int CompareTo(WaitingFall other) => Distance.CompareTo(other.Distance);
        }

        // Nearest the camera first; what the budget leaves waits, and past the wait it is dropped.
        private void MakeWaitingFalls()
        {
            // Everything fits the budget, so the order doesn't matter.
            if (_waiting.Length > _budget)
            {
                for (var i = 0; i < _waiting.Length; i++)
                {
                    var waiting = _waiting[i];
                    waiting.Distance = CameraDistance(waiting.Shown.World.c3.xyz);
                    _waiting[i] = waiting;
                }

                _waiting.Sort();
            }

            var kept = 0;
            for (var i = 0; i < _waiting.Length; i++)
            {
                var waiting = _waiting[i];
                if (_budget > 0)
                {
                    _budget--;
                    Fall(waiting);
                }
                else if (_now - waiting.Since <= FallWait)
                {
                    _waiting[kept++] = waiting;
                }
            }

            _waiting.Length = kept;
        }

        private void SortHandovers()
        {
            for (var i = 0; i < _handovers.Length; i++)
            {
                var handover = _handovers[i];
                var figure = EntityManager.GetBuffer<SquadFigure>(handover.Squad)[handover.Figure];
                handover.World = EntityManager.GetComponentData<LocalToWorld>(figure.Figure).Value;
                handover.Distance = CameraDistance(handover.World.c3.xyz);
                _handovers[i] = handover;
            }

            _handovers.Sort();
        }

        // Without a camera every figure is as near as any other.
        private float CameraDistance(float3 position) =>
            _camera == null ? 0f : math.distance(_cameraPosition, position);
    }
}
