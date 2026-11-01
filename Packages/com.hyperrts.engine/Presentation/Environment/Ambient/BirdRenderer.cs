using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Overlays;
using HyperRTS.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Draws every bird in one call as a camera-facing quad heading up the screen, each picking its wing pose in
    /// <c>HyperRTS/Bird</c>.
    /// </summary>
    public sealed class BirdRenderer : IDisposable
    {
        private readonly Mesh _quad = OverlayMeshes.Billboard();
        private readonly InstanceBuffer<BirdInstance> _birds;

        public BirdRenderer()
        {
            _birds = new InstanceBuffer<BirdInstance>(_quad);
        }

        public void Draw(IReadOnlyList<BirdFlock> flocks, Material material, float size, Vector3 eye)
        {
            foreach (var flock in flocks)
            {
                for (var bird = 0; bird < flock.Count; bird++)
                {
                    Vector3 position = flock.Position(bird);
                    var facing = Quaternion.LookRotation(position - eye, flock.Velocity(bird));
                    _birds.Add(new BirdInstance
                    {
                        ObjectToWorld = Matrix4x4.TRS(position, facing, Vector3.one * size),
                        Frame = (uint)flock.Frame(bird),
                    });
                }
            }

            _birds.Draw(material, 0);
        }

        public void Dispose()
        {
            _birds.Dispose();
            CoreUtils.Destroy(_quad);
        }
    }
}
