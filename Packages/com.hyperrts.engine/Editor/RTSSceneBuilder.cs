using System;
using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using Unity.NetCode;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor
{
    /// <summary>Creates a playable RTS scene (rig, light, ground, SubScene with Match and bases) from a spec.</summary>
    public static class RTSSceneBuilder
    {
        /// <summary>Builds and saves the scene and its SubScene; returns the SubScene's path.</summary>
        public static string Build(RTSSceneSpec spec)
        {
            Validate(spec);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            CreateGround(spec.MapSize);
            PrefabUtility.InstantiatePrefab(spec.Rig != null ? spec.Rig : EditorAssets.RigPrefab, scene);
            EditorSceneManager.SaveScene(scene, spec.ScenePath);

            var subScenePath = Path.ChangeExtension(spec.ScenePath, null) + "_Entities.unity";
            CreateSubScene(spec, subScenePath);
            var subScene = new GameObject("SubScene").AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            subScene.AutoLoadScene = true;
            EditorSceneManager.SaveScene(scene);
            return subScenePath;
        }

        /// <summary>Bases sit on a circle around the centre, player 1 at the bottom of the map.</summary>
        public static List<Vector3> BasePositions(Vector2 mapSize, int players)
        {
            var radius = Mathf.Min(mapSize.x, mapSize.y) * 0.35f;
            var positions = new List<Vector3>(players);
            for (var i = 0; i < players; i++)
            {
                var angle = -Mathf.PI * 0.5f + i * 2f * Mathf.PI / players;
                positions.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }

            return positions;
        }

        private static void Validate(RTSSceneSpec spec)
        {
            if (string.IsNullOrEmpty(spec.ScenePath) || !spec.ScenePath.EndsWith(".unity", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Scene path '{spec.ScenePath}' must be a project path ending in .unity.");
            }

            if (spec.Players < 1 || spec.Players >= FactionRelations.MaxTeams)
            {
                throw new ArgumentException($"Players must be 1 to {FactionRelations.MaxTeams - 1}, not {spec.Players}.");
            }

            if (spec.MapSize.x <= 0f || spec.MapSize.y <= 0f)
            {
                throw new ArgumentException($"Map size {spec.MapSize} must be positive.");
            }
        }

        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Unity's plane is 10 units wide.
        private static void CreateGround(Vector2 mapSize)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(mapSize.x / 10f, 1f, mapSize.y / 10f);
        }

        // The Match stays a plain object (its players become ghosts of their own); MatchState is the ghost.
        private static void CreateSubScene(RTSSceneSpec spec, string path)
        {
            var entities = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var match = new GameObject("Match").AddComponent<MatchAuthoring>();
            match.gameObject.AddComponent<NetCodePhysicsConfig>().PhysicGroupRunMode = PhysicGroupRunMode.AlwaysRun;
            EditorSceneManager.MoveGameObjectToScene(match.gameObject, entities);
            PrefabUtility.InstantiatePrefab(EditorAssets.MatchStatePrefab, entities);
            match.mapSize = spec.MapSize;
            AddPlayers(match, spec.Players);

            var bases = BasePositions(spec.MapSize, spec.Players);
            if (spec.StartingBase != null)
            {
                PlaceBases(entities, spec.StartingBase, bases);
            }

            spec.Furnish?.Invoke(new RTSSubScene(entities, match, bases));
            EditorSceneManager.SaveScene(entities, path);
            EditorSceneManager.CloseScene(entities, true);
        }

        private static void AddPlayers(MatchAuthoring match, int players)
        {
            match.players.Clear();
            for (var i = 0; i < players; i++)
            {
                match.players.Add(new PlayerSlot
                {
                    name = i == 0 ? "Player" : $"AI {i}",
                    team = i + 1,
                    color = PlayerSlot.Palette[i % PlayerSlot.Palette.Length],
                    control = i == 0 ? PlayerControl.LocalHuman : PlayerControl.AI,
                });
            }
        }

        private static void PlaceBases(Scene entities, GameEntityAuthoring startingBase, List<Vector3> bases)
        {
            for (var i = 0; i < bases.Count; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(startingBase.gameObject, entities);
                instance.transform.position = bases[i];
                var authoring = instance.GetComponent<GameEntityAuthoring>();
                authoring.owner = i + 1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(authoring);
            }
        }
    }
}
