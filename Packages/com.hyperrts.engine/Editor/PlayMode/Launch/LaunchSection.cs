using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Match;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>The Play page's launcher form: map, mode, start, network, the slot table and the game's own settings.</summary>
    internal class LaunchSection
    {
        private static readonly string[] SlotKinds = { "Closed", "Human", "Bot" };
        private static readonly string[] Modes = { "Free for all", "1v1", "2v2", "3v3", "4v4" };
        private static readonly string[] Entries = { "Straight into the map", "Through the main menu" };
        private static readonly string[] Networks = { "Offline", "Host", "Dedicated server" };
        private static readonly AIDifficulty[] Difficulties = (AIDifficulty[])Enum.GetValues(typeof(AIDifficulty));

        private readonly LaunchChoice _choice = EditorState.instance.launch;
        private readonly GameLauncher _game = GameLauncher.Find();
        private readonly VisualTreeAsset _slotRow;
        private readonly VisualElement _slots;
        private readonly HelpBox _problem;
        private readonly Button _play;
        private SliderInt _virtualPlayers;
        private IReadOnlyList<LaunchMap> _maps;
        private IReadOnlyList<LaunchSide> _sides;
        private LaunchMap _map;

        /// <summary>Wires a cloned LaunchSection.uxml.</summary>
        public LaunchSection(VisualElement root, VisualTreeAsset slotRow)
        {
            _slotRow = slotRow;
            _slots = root.Require<VisualElement>("slots");
            _problem = root.Require<HelpBox>("problem");
            _play = root.Require<Button>("play");
            var missing = root.Require<HelpBox>("missing");
            var form = root.Require<VisualElement>("form");
            var ready = _game != null && _game.Maps.Count > 0;
            missing.SetHidden(ready);
            form.SetHidden(!ready);
            if (!ready)
            {
                return;
            }

            _maps = _game.Maps;
            _sides = _game.Sides;
            if (!_game.TryFindMap(_choice.map, out _map))
            {
                _map = _maps[0];
                _choice.map = _map.Id;
                _choice.slots.Clear();
            }

            if (_choice.slots.Count != _map.Slots)
            {
                _choice.Deal(_map.Slots);
            }

            BindChoices(root);
            BindSettings(root.Require<VisualElement>("settings"));
            _play.clicked += () => _game.Play(_choice);
            RebuildSlots();
            Refresh();
        }

        private void BindChoices(VisualElement root)
        {
            var map = root.Require<DropdownField>("map");
            map.choices = _maps.Select(entry => entry.Name).ToList();
            map.SetValueWithoutNotify(_map.Name);
            map.RegisterValueChangedCallback(_ =>
            {
                _map = _maps[map.index];
                _choice.map = _map.Id;
                Deal();
            });

            var mode = Dropdown(root, "mode", Modes, (int)_choice.mode);
            mode.RegisterValueChangedCallback(_ =>
            {
                _choice.mode = (TeamLayout)mode.index;
                Deal();
            });

            var entry = Dropdown(root, "entry", Entries, (int)_choice.entry);
            entry.RegisterValueChangedCallback(_ =>
            {
                _choice.entry = (LaunchEntry)entry.index;
                Refresh();
            });

            var network = Dropdown(root, "network", Networks, (int)_choice.network);
            network.RegisterValueChangedCallback(_ =>
            {
                _choice.network = (LaunchNetwork)network.index;
                Refresh();
            });

            _virtualPlayers = root.Require<SliderInt>("virtual-players");
            _virtualPlayers.SetValueWithoutNotify(_choice.virtualPlayers);
            _virtualPlayers.RegisterValueChangedCallback(change =>
            {
                _choice.virtualPlayers = change.newValue;
                Refresh();
            });
        }

        private static void BindSettings(VisualElement host)
        {
            foreach (var setting in TypeDiscovery.Instances<LaunchSetting>())
            {
                var toggle = new Toggle(setting.Label) { value = setting.Enabled, tooltip = setting.Tooltip };
                toggle.RegisterValueChangedCallback(change => setting.Enabled = change.newValue);
                host.Add(toggle);
            }
        }

        private static DropdownField Dropdown(VisualElement root, string name, string[] choices, int index)
        {
            var field = root.Require<DropdownField>(name);
            field.choices = choices.ToList();
            field.index = index;
            return field;
        }

        private void Deal()
        {
            _choice.Deal(_map.Slots);
            RebuildSlots();
            Refresh();
        }

        private void RebuildSlots()
        {
            _slots.Clear();
            for (var i = 0; i < _choice.slots.Count; i++)
            {
                _slots.Add(BuildRow(i, _choice.slots[i]));
            }
        }

        private VisualElement BuildRow(int index, LaunchSlot slot)
        {
            var row = _slotRow.Instantiate();
            row.Require<Label>("number").text = (index + 1).ToString();
            var side = row.Require<DropdownField>("side");
            var team = row.Require<IntegerField>("team");
            var difficulty = row.Require<DropdownField>("difficulty");
            var kind = Dropdown(row, "kind", SlotKinds, (int)slot.kind);

            side.choices = _sides.Select(entry => entry.Name).ToList();
            side.index = Math.Max(0, _sides.ToList().FindIndex(entry => entry.Id == slot.side));
            side.RegisterValueChangedCallback(_ => Edit(() => slot.side = _sides[side.index].Id));
            team.SetValueWithoutNotify(slot.team);
            team.RegisterValueChangedCallback(change => Edit(() => slot.team = Math.Max(0, change.newValue)));
            difficulty.choices = Difficulties.Select(entry => entry.ToString()).ToList();
            difficulty.index = Array.IndexOf(Difficulties, slot.difficulty);
            difficulty.RegisterValueChangedCallback(_ => Edit(() => slot.difficulty = Difficulties[difficulty.index]));
            kind.RegisterValueChangedCallback(_ =>
            {
                Edit(() => slot.kind = (LaunchSlotKind)kind.index);
                ShowKind(slot, side, team, difficulty);
            });
            ShowKind(slot, side, team, difficulty);
            return row;
        }

        private static void ShowKind(LaunchSlot slot, VisualElement side, VisualElement team, VisualElement difficulty)
        {
            side.SetEnabled(slot.kind != LaunchSlotKind.Closed);
            team.SetEnabled(slot.kind != LaunchSlotKind.Closed);
            difficulty.SetInvisible(slot.kind != LaunchSlotKind.Bot);
        }

        private void Edit(Action change)
        {
            change();
            Refresh();
        }

        // Saves the choice, shows what blocks the start and lets the main toolbar show the new label.
        private void Refresh()
        {
            EditorState.instance.Save();
            var problem = _choice.Problem(_map.Slots);
            _problem.text = problem;
            _problem.SetHidden(problem.Length == 0);
            _play.SetEnabled(problem.Length == 0);
            _virtualPlayers.SetHidden(_choice.network != LaunchNetwork.Host || _choice.entry != LaunchEntry.Map);
            MainToolbar.Refresh(LaunchToolbar.Path);
        }
    }
}
