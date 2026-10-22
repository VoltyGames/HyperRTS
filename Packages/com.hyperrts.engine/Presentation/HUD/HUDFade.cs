using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A vertical fade from clear to <c>--fade-color</c> at the bottom, so text reads over a picture.</summary>
    [UxmlElement]
    public partial class HUDFade : VisualElement
    {
        private static readonly CustomStyleProperty<Color> ColorProperty = new("--fade-color");

        private Color _color = new(0f, 0f, 0f, 0.85f);

        public HUDFade()
        {
            AddToClassList("hud-fade");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        }

        private void OnStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(ColorProperty, out var color))
            {
                _color = color;
                MarkDirtyRepaint();
            }
        }

        private void Paint(MeshGenerationContext context)
        {
            var rect = new Rect(Vector2.zero, layout.size);
            if (rect.width <= 0f)
            {
                return;
            }

            var clear = new Color(_color.r, _color.g, _color.b, 0f);
            var mesh = context.Allocate(4, 6);
            mesh.SetNextVertex(Corner(rect.xMin, rect.yMin, clear));
            mesh.SetNextVertex(Corner(rect.xMax, rect.yMin, clear));
            mesh.SetNextVertex(Corner(rect.xMax, rect.yMax, _color));
            mesh.SetNextVertex(Corner(rect.xMin, rect.yMax, _color));

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
