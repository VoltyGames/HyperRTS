namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Exactly one Match once the open scenes hold a SubScene.</summary>
    public sealed class MatchCountRule : ISceneRule
    {
        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            if (scenes.Matches.Count > 1)
            {
                issues.Error(scenes.Matches[1], "More than one Match in the open scenes; keep exactly one.");
            }
            else if (scenes.Matches.Count == 0 && scenes.AnySubScene)
            {
                issues.Error(null, "No Match in the open SubScenes; add GameObject ▸ HyperRTS ▸ Match.");
            }
        }
    }
}
