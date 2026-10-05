namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A rule across the project's prefabs, run by the full validator only. Implementations are discovered.</summary>
    public interface IPrefabRule
    {
        void Check(PrefabSet prefabs, ValidationIssues issues);
    }
}
