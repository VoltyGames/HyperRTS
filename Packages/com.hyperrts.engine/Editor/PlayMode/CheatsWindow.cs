using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Cheats: resources, fog, instant build, spawning, player switching and game speed.</summary>
    public class CheatsWindow : EditorWindow
    {
        private const long PollMilliseconds = 250;

        private HelpBox _status;
        private VisualElement _controls;
        private PopupField<string> _player;
        private PopupField<string> _prefab;
        private IntegerField _amount;
        private Button _control;
        private Toggle _fog;
        private Slider _speed;

        [MenuItem(EditorMenu.Cheats, false, EditorMenu.CheatsPriority)]
        public static void Open() => GetWindow<CheatsWindow>("HyperRTS Cheats");

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.AddStyles(root);
            _status = new HelpBox("", HelpBoxMessageType.Info);
            _status.AddToClassList("hrts-note");
            root.Add(_status);

            _controls = new VisualElement();
            _controls.AddToClassList("hrts-note");
            BuildPlayerControls(_controls);
            BuildWorldControls(_controls);
            root.Add(_controls);

            root.schedule.Execute(Poll).Every(PollMilliseconds);
            Poll();
        }

        private void BuildPlayerControls(VisualElement parent)
        {
            _player = Popup("Player");
            parent.Add(_player);

            _amount = new IntegerField("Resources") { value = 1000 };
            parent.Add(Row(_amount, new Button(() => Act((manager, player, _) =>
                Cheats.AddResources(manager, player, _amount.value))) { text = "Add" }));

            _control = new Button(() => Act((manager, player, _) => Cheats.MakeLocal(manager, player)))
            {
                text = "Control this player",
            };
            var instantBuild = new Button(() => Act((manager, _, faction) => Cheats.InstantBuild(manager, faction)))
            {
                text = "Instant build",
            };
            var buttons = new VisualElement();
            buttons.AddToClassList("hrts-buttons");
            buttons.Add(instantBuild);
            buttons.Add(_control);
            parent.Add(buttons);
        }

        private void BuildWorldControls(VisualElement parent)
        {
            _fog = new Toggle("Fog of war");
            _fog.RegisterValueChangedCallback(change => Act((manager, _, _) => Cheats.SetFog(manager, change.newValue)));
            parent.Add(_fog);

            _prefab = Popup("Spawn");
            parent.Add(Row(_prefab, new Button(SpawnAtView) { text = "At view" }));

            _speed = new Slider("Game speed", 0f, 4f) { showInputField = true };
            _speed.RegisterValueChangedCallback(change => LocalGameSpeed.SetScale(change.newValue));
            parent.Add(_speed);
        }

        // Cheats change game state, so they go to the server when hosting, not to the client the HUD shows.
        private void Poll()
        {
            var ready = TryGetPlayers(out var manager, out var players, out var message);
            _status.text = message;
            _status.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            _controls.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ready)
            {
                return;
            }

            SetChoices(_player, PlayerLabels(manager, players));
            SetChoices(_prefab, PrefabNames(manager, Cheats.Prefabs(manager)));
            _control.SetEnabled(CanControl(manager, players[System.Math.Min(_player.index, players.Length - 1)]));
            _fog.SetValueWithoutNotify(Cheats.FogEnabled(manager));
            _speed.SetValueWithoutNotify(LocalGameSpeed.Scale);
        }

        private static bool TryGetPlayers(out EntityManager manager, out NativeArray<Entity> players, out string message)
        {
            players = default;
            if (!PlayWorld.TryGetAuthoritative(out manager))
            {
                message = "Cheats work in Play mode.";
                return false;
            }

            players = Cheats.Players(manager);
            message = players.Length == 0 ? "No players yet: the SubScene is still loading." : "";
            return players.Length > 0;
        }

        /// <summary>Runs a cheat on the selected player, re-read now since entities change between polls.</summary>
        private void Act(System.Action<EntityManager, Entity, byte> cheat)
        {
            if (!TryGetPlayers(out var manager, out var players, out _))
            {
                return;
            }

            var player = players[System.Math.Min(_player.index, players.Length - 1)];
            cheat(manager, player, manager.GetComponentData<Player>(player).Faction);
        }

        private void SpawnAtView() => Act((manager, _, faction) =>
        {
            var prefabs = Cheats.Prefabs(manager);
            if (_prefab.index >= 0 && _prefab.index < prefabs.Length)
            {
                Cheats.Spawn(manager, prefabs[_prefab.index], faction, Cheats.ViewCenter(manager));
            }
        });

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

        private static List<string> PrefabNames(EntityManager manager, NativeArray<Entity> prefabs)
        {
            var names = new List<string>(prefabs.Length);
            foreach (var prefab in prefabs)
            {
                names.Add(manager.GetComponentData<EntityInfo>(prefab).Name.ToString());
            }

            return names;
        }

        // Polls rebuild the lists; touching the field only on a real change keeps an open dropdown stable.
        private static void SetChoices(PopupField<string> field, List<string> choices)
        {
            if (choices.Count == 0 || choices.SequenceEqual(field.choices))
            {
                return;
            }

            var index = field.index >= 0 && field.index < choices.Count ? field.index : 0;
            field.choices = choices;
            field.index = index;
        }

        private static PopupField<string> Popup(string label) => new(label) { choices = new List<string>() };

        private static VisualElement Row(VisualElement field, Button button)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-row");
            field.AddToClassList("hrts-row__grow");
            row.Add(field);
            row.Add(button);
            return row;
        }
    }
}
