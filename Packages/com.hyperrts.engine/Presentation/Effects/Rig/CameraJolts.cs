using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Effects.Rig
{
    /// <summary>The jolts one presented frame's bursts give the camera, which reads them before the pool clears them.</summary>
    public sealed class CameraJolts
    {
        private readonly List<CameraJolt> _jolts = new();

        public IReadOnlyList<CameraJolt> All => _jolts;

        public void Add(Vector3 position, float strength) => _jolts.Add(new CameraJolt(position, strength));

        public void Clear() => _jolts.Clear();
    }
}
