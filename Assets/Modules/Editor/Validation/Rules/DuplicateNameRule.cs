using System.Linq;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Display names hash to the type id, so two prefabs sharing one merge into one type.</summary>
    public sealed class DuplicateNameRule : IPrefabRule
    {
        public void Check(PrefabSet prefabs, ValidationIssues issues)
        {
            foreach (var group in prefabs.Entities.GroupBy(prefab => prefab.TypeId).Where(group => group.Count() > 1))
            {
                var names = string.Join(", ", group.Select(prefab => prefab.gameObject.name));
                issues.Error(group.First(),
                    $"Prefabs {names} share the display name '{group.First().DisplayName}'; give each a unique name.");
            }
        }
    }
}
