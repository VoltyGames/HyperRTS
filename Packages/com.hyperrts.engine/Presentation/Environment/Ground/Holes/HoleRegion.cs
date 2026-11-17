using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Holes
{
    /// <summary>The texel box that covers every rectangle added to it.</summary>
    public struct HoleRegion
    {
        private RectInt _rect;
        private bool _any;

        public readonly bool IsEmpty => !_any;

        public readonly RectInt Rect => _rect;

        /// <summary>Grows the box to take in <paramref name="bounds"/>; an empty one adds nothing.</summary>
        public void Add(RectInt bounds)
        {
            if (bounds.width <= 0 || bounds.height <= 0)
            {
                return;
            }

            if (!_any)
            {
                _rect = bounds;
                _any = true;
                return;
            }

            var xMin = Mathf.Min(_rect.xMin, bounds.xMin);
            var yMin = Mathf.Min(_rect.yMin, bounds.yMin);
            _rect = new RectInt(xMin, yMin, Mathf.Max(_rect.xMax, bounds.xMax) - xMin,
                Mathf.Max(_rect.yMax, bounds.yMax) - yMin);
        }
    }
}
