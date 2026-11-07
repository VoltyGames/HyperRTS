using HyperRTS.Presentation.Animation.Machines;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Transport;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Inspector buttons that add the parts missing from Add Component, because they only live under a parent.</summary>
    public static class PartButtons
    {
        [InspectorButton("Add part", Glyph = "plus")]
        private static void AddMachinePart(MachineAuthoring machine) => AddChild<MachinePartAuthoring>(machine, "Machine Part");

        [InspectorButton("Add garrison part", Glyph = "plus")]
        private static void AddGarrisonPart(ContainerAuthoring container) => AddChild<GarrisonPartAuthoring>(container, "Garrison Part");

        private static void AddChild<T>(Component parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.AddComponent<T>();
        }
    }
}
