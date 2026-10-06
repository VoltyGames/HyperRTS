using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Base for layers that shade grid cells flat on the ground.</summary>
    public abstract class CellDebugLayer : DebugLayer
    {
        private const float Height = 0.1f;

        private static Material _material;

        protected static void BeginCells(Color color)
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_Cull", 0);
                _material.SetInt("_ZWrite", 0);
            }

            _material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(color);
        }

        protected static void Cell(float2 min, float size)
        {
            GL.Vertex3(min.x, Height, min.y);
            GL.Vertex3(min.x, Height, min.y + size);
            GL.Vertex3(min.x + size, Height, min.y + size);
            GL.Vertex3(min.x + size, Height, min.y);
        }

        protected static void EndCells() => GL.End();
    }
}
