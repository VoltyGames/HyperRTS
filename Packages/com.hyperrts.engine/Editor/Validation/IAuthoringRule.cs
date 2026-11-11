using HyperRTS.Editor.Validation.Issues;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>A validation rule for authoring components. Implementations are discovered, so games add their own.</summary>
    public interface IAuthoringRule
    {
        bool AppliesTo(Component component);

        void Check(Component component, ValidationIssues issues);
    }
}
