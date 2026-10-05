using System;
using HyperRTS.Simulation.Buildings;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Adds a draggable footprint box to the entity inspector.</summary>
    [CustomEditor(typeof(BuildingAuthoring), true)]
    [CanEditMultipleObjects]
    public class BuildingAuthoringEditor : GameEntityAuthoringEditor
    {
        protected override Action ShapeHandle(Vector3 center, Color color)
        {
            var building = (BuildingAuthoring)target;
            var footprint = RTSHandles.Footprint(center, building.footprint, color);
            return () => building.footprint = footprint;
        }
    }
}
