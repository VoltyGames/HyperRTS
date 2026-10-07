using System.Linq;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Cheats: resources, fog, instant build, spawning, player switching and game speed.</summary>
    public class CheatsWindow : EditorWindow
    {
        private int _player;
        private int _prefab;
        private int _amount = 1000;

        [MenuItem(EditorMenu.Cheats, false, EditorMenu.CheatsPriority)]
        public static void Open() => GetWindow<CheatsWindow>("HyperRTS Cheats");

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            // Cheats change game state, so they go to the server when hosting, not to the client the HUD shows.
            if (!PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                EditorGUILayout.HelpBox("Cheats work in Play mode.", MessageType.Info);
                return;
            }

            var players = Cheats.Players(entityManager);
            if (players.Length == 0)
            {
                EditorGUILayout.HelpBox("No players yet: the SubScene is still loading.", MessageType.Info);
                return;
            }

            var names = players.Select(entity => PlayerLabel(entityManager, entity)).ToArray();
            _player = EditorGUILayout.Popup("Player", Mathf.Min(_player, names.Length - 1), names);
            var player = players[_player];
            var faction = entityManager.GetComponentData<Player>(player).Faction;

            DrawPlayerCheats(entityManager, player, faction);
            DrawSpawn(entityManager, faction);

            EditorGUILayout.Space();
            var speed = EditorGUILayout.Slider("Game speed", LocalGameSpeed.Scale, 0f, 4f);
            if (!Mathf.Approximately(speed, LocalGameSpeed.Scale))
            {
                LocalGameSpeed.SetScale(speed);
            }
        }

        private void DrawPlayerCheats(EntityManager entityManager, Entity player, byte faction)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _amount = EditorGUILayout.IntField("Resources", _amount);
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    Cheats.AddResources(entityManager, player, _amount);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Instant build"))
                {
                    Cheats.InstantBuild(entityManager, faction);
                }

                using (new EditorGUI.DisabledScope(!CanControl(entityManager, player)))
                {
                    if (GUILayout.Button("Control this player"))
                    {
                        Cheats.MakeLocal(entityManager, player);
                    }
                }
            }

            var fog = Cheats.FogEnabled(entityManager);
            if (EditorGUILayout.Toggle("Fog of war", fog) != fog)
            {
                Cheats.SetFog(entityManager, !fog);
            }
        }

        private void DrawSpawn(EntityManager entityManager, byte faction)
        {
            var prefabs = Cheats.Prefabs(entityManager);
            if (prefabs.Length == 0)
            {
                return;
            }

            var names = prefabs.Select(entity => entityManager.GetComponentData<EntityInfo>(entity).Name.ToString()).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                _prefab = EditorGUILayout.Popup("Spawn", Mathf.Min(_prefab, names.Length - 1), names);
                if (GUILayout.Button("At view", GUILayout.Width(60)))
                {
                    Cheats.Spawn(entityManager, prefabs[_prefab], faction, Cheats.ViewCenter(entityManager));
                }
            }
        }

        // Over the network the server decides which connection controls a player.
        private static bool CanControl(EntityManager entityManager, Entity player) =>
            !NetworkSession.IsRunning && !entityManager.HasComponent<LocalPlayer>(player);

        private static string PlayerLabel(EntityManager entityManager, Entity player)
        {
            var name = entityManager.GetComponentData<Player>(player).Name.ToString();
            if (entityManager.HasComponent<LocalPlayer>(player))
            {
                return name + " (you)";
            }

            return entityManager.HasComponent<AIPlayer>(player) ? name + " (AI)" : name;
        }
    }
}
