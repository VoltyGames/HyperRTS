using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A vertical fade from clear to <c>--fade-color</c> at the bottom, so text reads over a picture.</summary>
    [UxmlElement]
    public partial class HUDFade : HUDPaintedElement
    {
        public HUDFade() : base("--fade-color", new Color(0f, 0f, 0f, 0.85f))
        {
        }

        protected override void Paint(MeshGenerationContext context)
        {
            var rect = new Rect(Vector2.zero, layout.size);
            if (rect.width <= 0f)
            {
                return;
            }

            var clear = new Color(PaintColor.r, PaintColor.g, PaintColor.b, 0f);
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(Corner(rect.xMin, rect.yMin, clear));
            mesh.SetNextVertex(Corner(rect.xMax, rect.yMin, clear));
            mesh.SetNextVertex(Corner(rect.xMax, rect.yMax, PaintColor));
            mesh.SetNextVertex(Corner(rect.xMin, rect.yMax, PaintColor));

            // Clockwise in panel space, which UI Toolkit requires.
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
        }

        private static Vertex Corner(float x, float y, Color tint) =>
            new() { position = new Vector3(x, y, Vertex.nearZ), tint = tint };
    }
}
