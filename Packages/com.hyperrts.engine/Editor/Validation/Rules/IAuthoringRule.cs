using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A validation rule for authoring components. Implementations are discovered, so games add their own.</summary>
    public interface IAuthoringRule
    {
        bool AppliesTo(Component component);

        void Check(Component component, ValidationIssues issues);
    }
}
