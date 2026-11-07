using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Rows
{
    /// <summary>What a Hierarchy row shows for one entity object: its component icon, owner colour and worst issue.</summary>
    internal readonly struct HierarchyRow
    {
        public readonly Texture icon;
        public readonly Color ownerColor;
        public readonly MessageType severity;

        public HierarchyRow(Texture icon, Color ownerColor, MessageType severity)
        {
            this.icon = icon;
            this.ownerColor = ownerColor;
            this.severity = severity;
        }

        public bool Decorated => icon != null;

        /// <summary>The marks for the row's right container: a severity dot when there are issues, then the icon.</summary>
        public VisualElement CreateMarks()
        {
            var marks = new VisualElement();
            marks.AddToClassList("hrts-row-marks");
            if (severity >= MessageType.Warning)
            {
                var dot = new VisualElement();
                dot.AddToClassList("hrts-row-marks__dot");
                dot.AddToClassList(severity == MessageType.Error ? "hrts-row-marks__dot--error" : "hrts-row-marks__dot--warning");
                marks.Add(dot);
            }

            var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("hrts-row-marks__icon");
            marks.Add(image);
            return marks;
        }

        /// <summary>The owner colour edge laid over the row's left side, or null for a neutral object.</summary>
        public VisualElement CreateOwnerEdge()
        {
            if (ownerColor.a <= 0f)
            {
                return null;
            }

            var edge = new VisualElement { pickingMode = PickingMode.Ignore };
            edge.AddToClassList("hrts-row-owner");
            // The colour is the owner's, data rather than style.
            edge.style.backgroundColor = ownerColor;
            return edge;
        }
    }
}
