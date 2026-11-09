using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Network.Session;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Replays;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>The Play-mode cheats: the chosen player's control, resources and builds; spawning, repair and battles; fog and game speed.</summary>
    internal class CheatsSection
    {
        private const long PollMilliseconds = 250;

        private readonly VisualElement _root;
        private readonly HelpBox _status;
        private readonly VisualElement _controls;
        private readonly DropdownField _player;
        private readonly Button _control;
        private readonly Button _prefab;
        private readonly Button _spawn;
        private readonly Button _battle;
        private readonly Button _repair;
        private readonly Button _destroy;
        private readonly Toggle _fog;
        private readonly Slider _speed;
        private readonly Button _pause;
        private readonly Label _speedNote;

        /// <summary>Wires the controls of a cloned CheatsSection.uxml and polls them while it is shown.</summary>
        public CheatsSection(VisualElement root)
        {
            _root = root;
            _status = root.Require<HelpBox>("status");
            _controls = root.Require<VisualElement>("controls");
            _player = root.Require<DropdownField>("player");
            _player.choices = new List<string>();
            _control = root.Require<Button>("control");
            _control.clicked += () => Act((manager, player, _) => MatchCheats.MakeLocal(manager, player));
            root.Require<Button>("add").clicked += () => Act((manager, player, _) =>
                MatchCheats.AddResources(manager, player, EditorState.instance.cheatResources));
            root.Require<Button>("instant-build").clicked +=
                () => Act((manager, _, owner) => MatchCheats.InstantBuild(manager, owner));

            _prefab = root.Require<Button>("prefab");
            _prefab.clicked += PickPrefab;
            _spawn = root.Require<Button>("spawn");
            _spawn.clicked += () => ActOnPrefab((manager, prefab, owner) =>
                MatchCheats.Spawn(manager, prefab, owner, MatchCheats.ViewCenter(manager), EditorState.instance.spawnCount));
            _battle = root.Require<Button>("battle");
            _battle.clicked += () => ActOnPrefab((manager, prefab, _) =>
                BattleCheat.Start(manager, prefab, EditorState.instance.armiesPerSide));
            _repair = root.Require<Button>("repair");
            _repair.clicked += () => Act((manager, _, _) => MatchCheats.Repair(manager, MatchCheats.Selection(manager)));
            _destroy = root.Require<Button>("destroy");
            _destroy.clicked += () => Act((manager, _, _) => MatchCheats.Destroy(manager, MatchCheats.Selection(manager)));

            _fog = root.Require<Toggle>("fog");
            _fog.RegisterValueChangedCallback(change => Act((manager, _, _) => MatchCheats.SetFog(manager, change.newValue)));
            _speed = root.Require<Slider>("speed");
            _speed.RegisterValueChangedCallback(change => LocalGameSpeed.SetScale(change.newValue));
            _pause = root.Require<Button>("pause");
            _pause.clicked += TogglePause;
            _speedNote = root.Require<Label>("speed-note");

            Poll();
            root.schedule.Execute(Poll).Every(PollMilliseconds);
        }

        // Cheats change game state, so they go to the server when hosting, not to the client the HUD shows.
        private void Poll()
        {
            var playing = EditorApplication.isPlaying;
            _root.SetHidden(!playing);
            if (!playing)
            {
                return;
            }

            var ready = TryGetPlayers(out var manager, out var players, out var message);
            _status.text = message;
            _status.SetHidden(string.IsNullOrEmpty(message));
            _controls.SetEnabled(ready);
            if (!ready)
            {
                return;
            }

            SetChoices(_player, PlayerLabels(manager, players));
            _control.SetEnabled(CanControl(manager, SelectedPlayer(players)));

            var prefabName = SpawnPicker.FindName(manager, EditorState.instance.spawnTypeId);
            _prefab.text = prefabName ?? "Choose a prefab";
            _spawn.SetEnabled(prefabName != null);
            _battle.SetEnabled(prefabName != null);

            var selected = MatchCheats.Selection(manager).Length;
            _repair.SetEnabled(selected > 0);
            _destroy.SetEnabled(selected > 0);
            _repair.text = selected > 0 ? $"Repair selected ({selected})" : "Repair selected";
            _destroy.text = selected > 0 ? $"Destroy selected ({selected})" : "Destroy selected";

            _fog.SetValueWithoutNotify(MatchCheats.FogEnabled(manager));
            var local = !NetworkSession.IsRunning;
            _speed.SetEnabled(local);
            _pause.SetEnabled(local);
            _speedNote.SetHidden(local);
            _speed.SetValueWithoutNotify(LocalGameSpeed.Scale);
            _pause.text = LocalGameSpeed.IsPaused ? "Resume" : "Pause";
        }

        // Inside Play only a still-loading SubScene needs a message.
        private static bool TryGetPlayers(out EntityManager manager, out NativeArray<Entity> players, out string message)
        {
            players = default;
            message = "";
            if (!PlayWorld.TryGetAuthoritative(out manager))
            {
                message = "No match is running.";
                return false;
            }

            players = PlayWorld.Players(manager);
            message = players.Length == 0 ? "No players yet: the SubScene is still loading." : "";
            return players.Length > 0;
        }

        /// <summary>Runs a cheat on the chosen player, re-read now since entities change between polls.</summary>
        private void Act(System.Action<EntityManager, Entity, byte> cheat)
        {
            if (!TryGetPlayers(out var manager, out var players, out _))
            {
                return;
            }

            var player = SelectedPlayer(players);
            cheat(manager, player, manager.GetComponentData<Player>(player).Owner);
            StopRecording(manager.World);
            Poll();
        }

        private void ActOnPrefab(System.Action<EntityManager, Entity, byte> cheat) => Act((manager, _, owner) =>
        {
            var prefab = MatchCheats.FindPrefab(manager, EditorState.instance.spawnTypeId);
            if (prefab != Entity.Null)
            {
                cheat(manager, prefab, owner);
            }
        });

        private void PickPrefab()
        {
            if (PlayWorld.TryGetAuthoritative(out var manager))
            {
                SpawnPicker.Open(manager, _prefab.worldBound, typeId =>
                {
                    EditorState.instance.spawnTypeId = typeId;
                    EditorState.instance.Save();
                    Poll();
                });
            }
        }

        private void TogglePause()
        {
            if (LocalGameSpeed.IsPaused)
            {
                LocalGameSpeed.Resume();
            }
            else
            {
                LocalGameSpeed.Pause();
            }

            Poll();
        }

        private Entity SelectedPlayer(NativeArray<Entity> players) =>
            players[System.Math.Clamp(_player.index, 0, players.Length - 1)];

        // Cheats change the world outside the recorded commands, so the recording would no longer play back.
        private static void StopRecording(World world)
        {
            if (ReplayRecorder.IsRecording(world))
            {
                ReplayRecorder.Discard(world);
                Debug.LogWarning("[HyperRTS] Cheats used: this match's replay recording was dropped.");
            }
        }

        // Over the network the server decides which connection controls a player.
        private static bool CanControl(EntityManager manager, Entity player) =>
            !NetworkSession.IsRunning && !manager.HasComponent<LocalPlayer>(player);

        private static List<string> PlayerLabels(EntityManager manager, NativeArray<Entity> players)
        {
            var labels = new List<string>(players.Length);
            foreach (var player in players)
            {
                var name = manager.GetComponentData<Player>(player).Name.ToString();
                if (manager.HasComponent<LocalPlayer>(player))
                {
                    name += " (you)";
                }
                else if (manager.HasComponent<AIPlayer>(player))
                {
                    name += " (AI)";
                }

                labels.Add(name);
            }

            return labels;
        }

        // Polls rebuild the list; touching the field only on a real change keeps an open dropdown stable.
        private static void SetChoices(DropdownField field, List<string> choices)
        {
            if (choices.Count == 0 || choices.SequenceEqual(field.choices))
            {
                return;
            }

            var index = field.index >= 0 && field.index < choices.Count ? field.index : 0;
            field.choices = choices;
            field.index = index;
        }
    }
}
