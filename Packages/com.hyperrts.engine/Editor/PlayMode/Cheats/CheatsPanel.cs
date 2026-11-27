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

namespace HyperRTS.Editor.PlayMode.Cheats;

/// <summary>The Play-mode cheats: the chosen player's control, resources and builds; spawning, repair and battles; fog and game speed.</summary>
internal class CheatsPanel
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
    private readonly Button _readySelected;
    private readonly Button _destroy;
    private readonly Toggle _fog;
    private readonly Slider _speed;
    private readonly Button _pause;
    private readonly Label _speedNote;

    /// <summary>Wires the controls of a cloned CheatsPanel.uxml and polls them while it is shown.</summary>
    public CheatsPanel(VisualElement root)
    {
        _root = root;
        _status = root.Require<HelpBox>("status");
        _controls = root.Require<VisualElement>("controls");
        _player = root.Require<DropdownField>("player");
        _player.choices = [];
        _control = root.Require<Button>("control");
        _control.clicked += () => Act((manager, player, _) => MatchCheats.MakeLocal(manager, player));
        root.Require<Button>("add").clicked += () => Act((manager, player, _) =>
            MatchCheats.AddResources(manager, player, PlayModeState.instance.cheatResources));
        root.Require<Button>("instant-build").clicked +=
            () => Act((manager, _, owner) => MatchCheats.InstantBuild(manager, owner));

        _prefab = root.Require<Button>("prefab");
        _prefab.clicked += PickPrefab;
        _spawn = root.Require<Button>("spawn");
        _battle = root.Require<Button>("battle");
        _repair = root.Require<Button>("repair");
        _destroy = root.Require<Button>("destroy");
        _readySelected = root.Require<Button>("ready-selected");
        WireSpawnAndCombat();
        new StrikesPanel(root.Require<VisualElement>("strikes"), this);

        _fog = root.Require<Toggle>("fog");
        _fog.RegisterValueChangedCallback(change => Act((manager, _, _) => MatchCheats.SetFog(manager, change.newValue)));
        _speed = root.Require<Slider>("speed");
        _speed.RegisterValueChangedCallback(change => ChangeSpeed(speed => speed.WithScale(change.newValue)));
        _pause = root.Require<Button>("pause");
        _pause.clicked += TogglePause;
        _speedNote = root.Require<Label>("speed-note");

        Poll();
        root.schedule.Execute(Poll).Every(PollMilliseconds);
    }

    private void WireSpawnAndCombat()
    {
        _spawn.clicked += () => ActOnPrefab((manager, prefab, owner) =>
            SpawnCheat.Spawn(manager, prefab, owner, PlayModeState.instance.spawnCount));
        _battle.clicked += () => ActOnPrefab((manager, prefab, _) =>
            BattleCheat.Start(manager, prefab, PlayModeState.instance.armiesPerSide));
        _repair.clicked += () => Act((manager, _, _) => MatchCheats.Repair(manager, MatchCheats.Selection(manager)));
        _destroy.clicked += () => Act((manager, _, _) => MatchCheats.Destroy(manager, MatchCheats.Selection(manager)));
        _readySelected.clicked += () =>
            Act((manager, _, _) => MatchCheats.ReadyAbilities(manager, MatchCheats.Selection(manager)));
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

        var prefabName = SpawnPicker.FindName(manager, PlayModeState.instance.spawnTypeId);
        _prefab.text = prefabName ?? "Choose a prefab";
        _spawn.SetEnabled(prefabName != null);
        _battle.SetEnabled(prefabName != null);

        PollSelection(manager);
        _fog.SetValueWithoutNotify(MatchCheats.FogEnabled(manager));
        PollSpeed(manager);
    }

    private void PollSelection(EntityManager manager)
    {
        var selected = MatchCheats.Selection(manager).Length;
        _repair.SetEnabled(selected > 0);
        _destroy.SetEnabled(selected > 0);
        _readySelected.SetEnabled(selected > 0);
        _repair.text = selected > 0 ? $"Repair selected ({selected})" : "Repair selected";
        _destroy.text = selected > 0 ? $"Destroy selected ({selected})" : "Destroy selected";
    }

    private void PollSpeed(EntityManager manager)
    {
        var local = !NetworkSession.IsRunning;
        _speed.SetEnabled(local);
        _pause.SetEnabled(local);
        _speedNote.SetHidden(local);
        if (local && PlayWorld.TryGetSingleton(manager, out GameSpeed speed))
        {
            _speed.SetValueWithoutNotify(speed.Scale);
            _pause.text = speed.Paused ? "Resume" : "Pause";
        }
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

    /// <summary>The player picked in the window, re-read now since entities change between polls.</summary>
    public bool TryGetChosen(out ChosenPlayer chosen)
    {
        chosen = default;
        if (!TryGetPlayers(out var manager, out var players, out _))
        {
            return false;
        }

        var player = SelectedPlayer(players);
        chosen = new ChosenPlayer(manager, player, manager.GetComponentData<Player>(player).Owner);
        return true;
    }

    // Cheats change the world outside the recorded commands, so the recording would no longer play back.
    private static void StopRecording(World world)
    {
        if (ReplayRecorder.IsRecording(world))
        {
            ReplayRecorder.Discard(world);
            Debug.LogWarning("[HyperRTS] Cheats used: this match's replay recording was dropped.");
        }
    }

    /// <summary>Runs <paramref name="cheat"/> on the chosen player's world, player and owner, then drops the replay recording.</summary>
    public void Act(System.Action<EntityManager, Entity, byte> cheat)
    {
        if (!TryGetChosen(out var chosen))
        {
            return;
        }

        cheat(chosen.Manager, chosen.Player, chosen.Owner);
        StopRecording(chosen.Manager.World);
        Poll();
    }

    private void ActOnPrefab(System.Action<EntityManager, Entity, byte> cheat) => Act((manager, _, owner) =>
    {
        var prefab = SpawnCheat.FindPrefab(manager, PlayModeState.instance.spawnTypeId);
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
                PlayModeState.instance.spawnTypeId = typeId;
                PlayModeState.instance.Save();
                Poll();
            });
        }
    }

    private void TogglePause() => ChangeSpeed(speed => speed.WithPaused(!speed.Paused));

    private void ChangeSpeed(System.Func<GameSpeed, GameSpeed> change)
    {
        var found = PlayWorld.TryGetAuthoritative(out var manager);
        if (found && PlayWorld.TryGetSingleton(manager, out GameSpeed speed))
        {
            PlayWorld.SetSingleton(manager, change(speed));
        }

        Poll();
    }

    private Entity SelectedPlayer(NativeArray<Entity> players) =>
        players[System.Math.Clamp(_player.index, 0, players.Length - 1)];

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
