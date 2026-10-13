using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One frame's selection rings and health bars that share a pair of materials.</summary>
    public sealed class UnitOverlayBatches
    {
        private readonly InstanceBatch _ownRings;
        private readonly InstanceBatch _enemyRings;
        private readonly InstanceBatch _neutralRings;
        private readonly InstanceBatch _barBacks;
        private readonly InstanceBatch _barsHigh;
        private readonly InstanceBatch _barsMid;
        private readonly InstanceBatch _barsLow;

        public UnitOverlayBatches(Mesh ring, Mesh billboard)
        {
            _ownRings = new InstanceBatch(ring);
            _enemyRings = new InstanceBatch(ring);
            _neutralRings = new InstanceBatch(ring);
            _barBacks = new InstanceBatch(billboard);
            _barsHigh = new InstanceBatch(billboard);
            _barsMid = new InstanceBatch(billboard);
            _barsLow = new InstanceBatch(billboard);
        }

        public void AddRing(Relation relation, in Matrix4x4 matrix) => RingBatch(relation).Add(matrix);

        public void AddBar(Vector3 center, float width, float fraction, float height, Transform camera)
        {
            var rotation = camera.rotation;
            _barBacks.Add(Matrix4x4.TRS(center, rotation, new Vector3(width, height, 1f)));

            var fillCenter = center - camera.right * ((1f - fraction) * width * 0.5f);
            var fills = fraction > 0.6f ? _barsHigh : fraction > 0.3f ? _barsMid : _barsLow;
            fills.Add(Matrix4x4.TRS(fillCenter, rotation, new Vector3(width * fraction, height, 1f)));
        }

        /// <summary><paramref name="front"/> renders after <paramref name="material"/> so fills stay above backgrounds.</summary>
        public void Draw(Material material, Material front, OverlayStyle style, int layer)
        {
            _ownRings.Draw(material, style.ownRing, layer);
            _enemyRings.Draw(material, style.enemyRing, layer);
            _neutralRings.Draw(material, style.neutralRing, layer);
            _barBacks.Draw(material, style.healthBack, layer);
            _barsHigh.Draw(front, style.healthHigh, layer);
            _barsMid.Draw(front, style.healthMid, layer);
            _barsLow.Draw(front, style.healthLow, layer);
        }

        private InstanceBatch RingBatch(Relation relation) => relation switch
        {
            Relation.Own or Relation.Ally => _ownRings,
            Relation.Enemy => _enemyRings,
            _ => _neutralRings,
        };
    }
}
