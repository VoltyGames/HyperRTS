using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor canvas's pan and zoom: map metres (X east, Z north) to canvas pixels (Y down) and back.
    /// </summary>
    public sealed class MapCanvasView
    {
        private const float MinScale = 0.05f;
        private const float MaxScale = 50f;
        private const float FitMargin = 0.95f;

        /// <summary>The map point at the canvas centre.</summary>
        public Vector2 Pan { get; private set; }

        /// <summary>Pixels per metre.</summary>
        public float Scale { get; private set; } = 1f;

        public Vector2 Size { get; set; }

        public Vector2 ToLocal(Vector2 world) =>
            Size * 0.5f + new Vector2(world.x - Pan.x, Pan.y - world.y) * Scale;

        public Vector2 ToWorld(Vector2 local)
        {
            var offset = (local - Size * 0.5f) / Scale;
            return new Vector2(Pan.x + offset.x, Pan.y - offset.y);
        }

        /// <summary>A map rectangle in canvas pixels (its north edge on top).</summary>
        public Rect ToLocal(Rect world)
        {
            var topLeft = ToLocal(new Vector2(world.xMin, world.yMax));
            return new Rect(topLeft, world.size * Scale);
        }

        /// <summary>Zooms by <paramref name="factor"/>, keeping the map point under <paramref name="local"/> still.</summary>
        public void ZoomAt(Vector2 local, float factor)
        {
            var anchor = ToWorld(local);
            Scale = Mathf.Clamp(Scale * factor, MinScale, MaxScale);
            Pan += anchor - ToWorld(local);
        }

        public void PanBy(Vector2 localDelta) => Pan += new Vector2(-localDelta.x, localDelta.y) / Scale;

        public void Fit(Rect world)
        {
            if (Size.x <= 0f || Size.y <= 0f || world.width <= 0f || world.height <= 0f)
            {
                return;
            }

            Pan = world.center;
            Scale = Mathf.Clamp(Mathf.Min(Size.x / world.width, Size.y / world.height) * FitMargin, MinScale, MaxScale);
        }
    }
}
