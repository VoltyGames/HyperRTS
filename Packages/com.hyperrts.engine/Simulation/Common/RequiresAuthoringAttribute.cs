using System;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// An authoring component this one needs on the same object. Unlike <c>[RequireComponent]</c> it only warns,
    /// so an abstract base such as <c>GameEntityAuthoring</c> can be required.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresAuthoringAttribute : Attribute
    {
        public RequiresAuthoringAttribute(Type type, string description)
        {
            Type = type;
            Description = description;
        }

        public Type Type { get; }

        /// <summary>How the warning names the requirement, e.g. "a Building".</summary>
        public string Description { get; }
    }
}
