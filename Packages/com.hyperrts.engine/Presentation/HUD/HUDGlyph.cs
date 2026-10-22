using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>A vector badge (padlock, lightning, chevron) drawn in the element's text colour, so it needs no art file.</summary>
    [UxmlElement]
    public partial class HUDGlyph : VisualElement
    {
        // The bolt as fractions of the box, from its top tip round its outline.
        private static readonly Vector2[] Bolt =
        {
            new(0.62f, 0f), new(0.22f, 0.56f), new(0.48f, 0.56f), new(0.36f, 1f), new(0.8f, 0.4f), new(0.54f, 0.4f),
        };

        private static readonly Vector2[] ChevronRight = { new(0.3f, 0.1f), new(0.7f, 0.5f), new(0.3f, 0.9f) };
        private static readonly Vector2[] ChevronLeft = { new(0.7f, 0.1f), new(0.3f, 0.5f), new(0.7f, 0.9f) };

        private HUDGlyphShape _shape;

        public HUDGlyph() : this(HUDGlyphShape.Padlock)
        {
        }

        public HUDGlyph(HUDGlyphShape shape)
        {
            _shape = shape;
            AddToClassList("hud-glyph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        [UxmlAttribute]
        public HUDGlyphShape Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                MarkDirtyRepaint();
            }
        }

        private void Paint(MeshGenerationContext context)
        {
            var box = contentRect;
            if (box.width <= 0f || box.height <= 0f)
            {
                return;
            }

            var painter = context.painter2D;
            painter.fillColor = resolvedStyle.color;
            painter.strokeColor = resolvedStyle.color;
            switch (_shape)
            {
                case HUDGlyphShape.Padlock:
                    PaintPadlock(painter, box);
                    break;
                case HUDGlyphShape.Lightning:
                    PaintBolt(painter, box);
                    break;
                case HUDGlyphShape.ChevronRight:
                    PaintChevron(painter, box, ChevronRight);
                    break;
                case HUDGlyphShape.ChevronLeft:
                    PaintChevron(painter, box, ChevronLeft);
                    break;
            }
        }

        private static void PaintBolt(Painter2D painter, Rect box)
        {
            painter.BeginPath();
            painter.MoveTo(Point(box, Bolt[0]));
            for (var i = 1; i < Bolt.Length; i++)
            {
                painter.LineTo(Point(box, Bolt[i]));
            }

            painter.ClosePath();
            painter.Fill();
        }

        private static void PaintChevron(Painter2D painter, Rect box, Vector2[] points)
        {
            painter.lineWidth = Mathf.Max(2f, box.width * 0.16f);
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(Point(box, points[0]));
            painter.LineTo(Point(box, points[1]));
            painter.LineTo(Point(box, points[2]));
            painter.Stroke();
        }

        private static void PaintPadlock(Painter2D painter, Rect box)
        {
            var body = Rect.MinMaxRect(box.xMin + box.width * 0.12f, box.yMin + box.height * 0.45f,
                box.xMax - box.width * 0.12f, box.yMax);
            var shackle = new Vector2(body.center.x, box.yMin + box.height * 0.32f);
            painter.lineWidth = Mathf.Max(1.5f, box.width * 0.13f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(shackle.x - body.width * 0.3f, body.yMin));
            painter.Arc(shackle, body.width * 0.3f, Angle.Degrees(180f), Angle.Degrees(360f));
            painter.LineTo(new Vector2(shackle.x + body.width * 0.3f, body.yMin));
            painter.Stroke();

            painter.BeginPath();
            painter.MoveTo(new Vector2(body.xMin, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMin));
            painter.LineTo(new Vector2(body.xMax, body.yMax));
            painter.LineTo(new Vector2(body.xMin, body.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static Vector2 Point(Rect box, Vector2 fraction) =>
            new(box.xMin + box.width * fraction.x, box.yMin + box.height * fraction.y);
    }

    /// <summary>What a <see cref="HUDGlyph"/> draws.</summary>
    public enum HUDGlyphShape
    {
        Padlock,
        Lightning,
        ChevronRight,
        ChevronLeft,
    }
}
