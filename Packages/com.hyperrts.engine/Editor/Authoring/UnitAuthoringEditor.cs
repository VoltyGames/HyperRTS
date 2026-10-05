using System;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Adds a draggable radius handle to the entity inspector.</summary>
    [CustomEditor(typeof(UnitAuthoring), true)]
    [CanEditMultipleObjects]
    public class UnitAuthoringEditor : GameEntityAuthoringEditor
    {
        protected override Action ShapeHandle(Vector3 center, Color color)
        {
            var unit = (UnitAuthoring)target;
            var radius = GroundHandles.Radius(center, unit.radius, color, "Radius");
            return () => unit.radius = Mathf.Max(0.05f, radius);
        }
    }
}
