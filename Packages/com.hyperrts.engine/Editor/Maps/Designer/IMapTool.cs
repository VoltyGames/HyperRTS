using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// One Map Editor canvas tool (Select, Point, Line, Area). The canvas sends it left-button and key events in canvas
    /// pixels, except panning, zooming and tool switching, and lets it draw over the features.
    /// </summary>
    public interface IMapTool
    {
        void Down(Vector2 local, EventModifiers modifiers, int clickCount);

        void Move(Vector2 local);

        void Up();

        /// <summary>Handles a key; false lets the canvas use it (Escape then returns to Select).</summary>
        bool Key(KeyCode key);

        /// <summary>Drops anything half-made; called when another tool takes over.</summary>
        void Cancel();

        void Paint(Painter2D painter);
    }
}
