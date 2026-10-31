namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A rule across the project's assets, run by the full validator after the prefab rules. Implementations are discovered.</summary>
    public interface IAssetRule
    {
        void Check(AssetSet assets, ValidationIssues issues);
    }
}
