using System;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>
    /// Draws the method as a button under the authoring inspector's fields; each click is one undo step. Put it on a
    /// parameterless method of the component, or on a static editor method that takes the component.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InspectorButtonAttribute : Attribute
    {
        public InspectorButtonAttribute(string label)
        {
            Label = label;
        }

        public string Label { get; }
    }
}
