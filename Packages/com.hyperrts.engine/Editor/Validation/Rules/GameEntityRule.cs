using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Clickability, visibility, cost, prerequisites and SubScene placement of units and buildings.</summary>
    public sealed class GameEntityRule : AuthoringRule<GameEntityAuthoring>
    {
        protected override void Check(GameEntityAuthoring entity, ValidationIssues issues)
        {
            FitColliderIfMissing(entity, "this can't be clicked, selected or right-click targeted.", issues);

            if (entity.GetComponentInChildren<Renderer>() == null)
            {
                issues.Info(entity, "No renderer: this will be invisible.");
            }

            CheckEmptyEntries(entity, entity.cost, quantity => quantity.type == null,
                "A cost entry has no resource type and will be ignored.", issues);
            CheckPrefabReference(entity, entity.deathSpawn, "Death spawn", issues);

            if (entity.prerequisites.Exists(prerequisite => prerequisite != null && prerequisite is not BuildingAuthoring))
            {
                issues.Warn(entity, "Prerequisites must be buildings; a unit prerequisite is never met.");
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
