namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A rule across the open scenes, run by the validator and on entering Play mode. Implementations are discovered.</summary>
    public interface ISceneRule
    {
        void Check(SceneSet scenes, ValidationIssues issues);
    }
}
