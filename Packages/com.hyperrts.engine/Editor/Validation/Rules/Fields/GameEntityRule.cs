using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Fields
{
    /// <summary>Clickability, visibility, prerequisites and SubScene placement of units and buildings.</summary>
    public sealed class GameEntityRule : AuthoringRule<GameEntityAuthoring>
    {
        protected override void Check(GameEntityAuthoring entity, ValidationIssues issues)
        {
            FitPickBoxIfMissing(entity, "this can't be clicked, selected or right-click targeted.", issues);

            if (entity.GetComponentInChildren<Renderer>() == null)
            {
                issues.Warn(entity, "No renderer: this will be invisible.");
            }

            if (entity.prerequisites.Exists(prerequisite => prerequisite is UnitAuthoring))
            {
                issues.Warn(entity, "A unit prerequisite is never met; require buildings or research.");
            }

            if (IsInPlainScene(entity.gameObject))
            {
                issues.Warn(entity, "Entities only bake inside a SubScene. Move this into the scene's SubScene.");
            }
        }

        // Prefab assets and Prefab Mode have no SubScene to be in.
        private static bool IsInPlainScene(GameObject go)
        {
            if (EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            {
                return false;
            }

            var inPrefabMode = PrefabStageUtility.GetPrefabStage(go) != null;
            return !inPrefabMode && !go.scene.isSubScene;
        }
    }
}
