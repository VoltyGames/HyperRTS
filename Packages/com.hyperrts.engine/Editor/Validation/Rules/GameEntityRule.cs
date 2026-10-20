using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Clickability, visibility, cost, prerequisites, death spawn and SubScene placement of units and buildings.</summary>
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
            CheckDeathSpawn(entity, issues);

            if (entity.prerequisites.Exists(prerequisite => prerequisite is UnitAuthoring))
            {
                issues.Warn(entity, "A unit prerequisite is never met; require buildings or research.");
            }

            if (IsInPlainScene(entity.gameObject))
            {
                issues.Warn(entity, "Entities only bake inside a SubScene. Move this into the scene's SubScene.");
            }
        }

        /// <summary>The server spawns a ghost's death spawn (wreck, ruins), so it must be a ghost for clients to see it.</summary>
        private static void CheckDeathSpawn(GameEntityAuthoring entity, ValidationIssues issues)
        {
            var spawn = entity.deathSpawn;
            CheckPrefabReference(entity, spawn, "Death spawn", issues);
            if (spawn != null && IsGhost(entity))
            {
                WarnIfNotGhost(entity, spawn, $"Death spawn '{spawn.name}' is not a ghost: clients won't see it.", issues);
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
