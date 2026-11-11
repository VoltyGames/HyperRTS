using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Quality;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>The corpses a frame may make, the figures waiting for them and the camera that orders them.</summary>
    internal sealed class CorpseBudget : IDisposable
    {
        public NativeList<CorpseHandover> Handovers = new(Allocator.Persistent);
        public NativeList<WaitingFall> Waiting = new(Allocator.Persistent);
        public Camera Camera;
        public float3 CameraPosition;
        public int Left;

        public void Begin()
        {
            Camera = MainCamera.Find(Camera);
            CameraPosition = Camera == null ? default : (float3)Camera.transform.position;
            Left = EffectDetail.CorpsesPerFrameAt(PresentationQuality.Current.Effects);
        }

        // Without a camera every figure is as near as any other.
        public float DistanceTo(float3 position) => Camera == null ? 0f : math.distance(CameraPosition, position);

        // Everything fits the budget, so the order doesn't matter.
        public void SortWaiting()
        {
            if (Waiting.Length <= Left)
            {
                return;
            }

            for (var i = 0; i < Waiting.Length; i++)
            {
                var waiting = Waiting[i];
                waiting.Distance = DistanceTo(waiting.Shown.World.c3.xyz);
                Waiting[i] = waiting;
            }

            Waiting.Sort();
        }

        public void SortHandovers()
        {
            for (var i = 0; i < Handovers.Length; i++)
            {
                var handover = Handovers[i];
                handover.Distance = DistanceTo(handover.World.c3.xyz);
                Handovers[i] = handover;
            }

            Handovers.Sort();
        }

        public void Dispose()
        {
            Handovers.Dispose();
            Waiting.Dispose();
        }
    }
}
