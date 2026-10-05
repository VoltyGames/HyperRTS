using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Validation
{
    /// <summary>Checks one authoring component on its own; shared by inspectors and the project validator.</summary>
    public static class AuthoringChecks
    {
        public static List<ValidationIssue> For(Component component)
        {
            var issues = new List<ValidationIssue>();
            switch (component)
            {
                case GameEntityAuthoring entity: CheckEntity(entity, issues); break;
                case ProducerAuthoring producer: CheckProducer(producer, issues); break;
                case BuilderAuthoring builder: CheckOptions(builder, builder.buildOptions, "Build option", issues); break;
                case WeaponAuthoring weapon: Require<GameEntityAuthoring>(weapon, "a Unit or Building", issues); break;
                case HarvesterAuthoring harvester: Require<UnitAuthoring>(harvester, "a Unit", issues); break;
                case ResourceDropOffAuthoring dropOff: Require<BuildingAuthoring>(dropOff, "a Building", issues); break;
                case ResourceNodeAuthoring node: CheckNode(node, issues); break;
                case ArmorAuthoring armor: CheckArmor(armor, issues); break;
                case MatchAuthoring match: CheckMatch(match, issues); break;
            }

            return issues;
        }

        /// <summary>True for objects in an edited scene (not a prefab asset or Prefab Mode).</summary>
        public static bool IsInScene(GameObject go) =>
            !EditorUtility.IsPersistent(go) && go.scene.IsValid() && PrefabStageUtility.GetPrefabStage(go) == null;

        private static void CheckEntity(GameEntityAuthoring entity, List<ValidationIssue> issues)
        {
            if (entity.GetComponentInChildren<Collider>() == null)
            {
                Warn(issues, entity, "No collider: this can't be clicked, selected or right-click targeted.");
            }

            if (entity.GetComponentInChildren<Renderer>() == null)
            {
                issues.Add(new ValidationIssue(MessageType.Info, "No renderer: this will be invisible.", entity));
            }

            if (entity.cost.Exists(quantity => quantity.type == null))
            {
                Warn(issues, entity, "A cost entry has no resource type and will be ignored.");
            }

            if (entity.prerequisites.Exists(prerequisite => prerequisite != null && prerequisite is not BuildingAuthoring))
            {
                Warn(issues, entity, "Prerequisites must be buildings; a unit prerequisite is never met.");
            }

            if (IsInScene(entity.gameObject) && !entity.gameObject.scene.isSubScene)
            {
                Warn(issues, entity, "Entities only bake inside a SubScene. Move this into the scene's SubScene.");
            }
        }

        private static void CheckProducer(ProducerAuthoring producer, List<ValidationIssue> issues)
        {
            Require<BuildingAuthoring>(producer, "a Building", issues);
            CheckOptions(producer, producer.productionOptions, "Production option", issues);

            if (producer.TryGetComponent(out BuildingAuthoring building) &&
                Mathf.Abs(producer.spawnOffset.x) < building.footprint.x * 0.5f &&
                Mathf.Abs(producer.spawnOffset.z) < building.footprint.y * 0.5f)
            {
                Warn(issues, producer, "Spawn offset is inside the footprint; units will spawn blocked.");
            }
        }

        private static void CheckOptions<T>(Component owner, List<T> options, string label, List<ValidationIssue> issues)
            where T : Object
        {
            foreach (var option in options)
            {
                if (option == null)
                {
                    Warn(issues, owner, $"{label} is empty and will be ignored.");
                }
                else if (!PrefabUtility.IsPartOfPrefabAsset(option))
                {
                    Warn(issues, owner, $"{label} '{option.name}' is a scene object; reference the prefab asset.");
                }
            }
        }

        private static void CheckNode(ResourceNodeAuthoring node, List<ValidationIssue> issues)
        {
            if (node.type == null)
            {
                issues.Add(new ValidationIssue(MessageType.Error, "Resource node has no resource type.", node));
            }

            if (node.GetComponentInChildren<Collider>() == null)
            {
                Warn(issues, node, "No collider: harvesters can't be right-click ordered to gather here.");
            }
        }

        private static void CheckArmor(ArmorAuthoring armor, List<ValidationIssue> issues)
        {
            if (armor.modifiers.Exists(entry => entry.damageType == null))
            {
                Warn(issues, armor, "An armor entry has no damage type and will be ignored.");
            }
        }

        private static void CheckMatch(MatchAuthoring match, List<ValidationIssue> issues)
        {
            if (match.players.Count == 0)
            {
                issues.Add(new ValidationIssue(MessageType.Error, "The match has no players.", match));
            }

            if (match.players.FindAll(player => player.control == PlayerControl.LocalHuman).Count > 1)
            {
                Warn(issues, match, "More than one player is Local Human; only one can be controlled.");
            }

            if (match.players.Exists(player => player.team == 0))
            {
                Warn(issues, match, "Team 0 is neutral; give every player a team of 1 or more.");
            }
        }

        private static void Require<T>(Component component, string what, List<ValidationIssue> issues) where T : Component
        {
            if (!component.TryGetComponent<T>(out _))
            {
                Warn(issues, component, $"{ObjectNames.NicifyVariableName(component.GetType().Name)} needs {what} on the same object.");
            }
        }

        private static void Warn(List<ValidationIssue> issues, Object context, string message) =>
            issues.Add(new ValidationIssue(MessageType.Warning, message, context));
    }
}
