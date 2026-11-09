using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Entities;
using Unity.Netcode;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Cheats &amp; Debug: the match at a glance, the cheats and the debug layers, in a window that docks beside the Game view.</summary>
    public class CheatsDebugWindow : EditorWindow
    {
        private const string WindowTitle = "Cheats & Debug";

        private const long PollMilliseconds = 500;

        private Label _match;

        [MenuItem(EditorMenu.CheatsAndDebug, false, EditorMenu.CheatsAndDebugPriority)]
        public static void Open() => GetWindow<CheatsDebugWindow>(WindowTitle);

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(CheatsDebugWindow)).CloneTree(root);
            EditorState.Bind(root);
            root.Query<Foldout>().ForEach(RememberOpen);
            new CheatsSection(root.Require<VisualElement>("cheats"));
            new DebugSection(root.Require<VisualElement>("debug"));

            _match = root.Require<Label>("match");
            ShowMatch();
            root.schedule.Execute(ShowMatch).Every(PollMilliseconds);
        }

        private static void RememberOpen(Foldout foldout)
        {
            var key = $"{WindowTitle}/{foldout.name}";
            foldout.SetValueWithoutNotify(!EditorState.instance.IsGroupClosed(key));
            foldout.RegisterValueChangedCallback(change =>
            {
                // Toggles inside the foldout send their own change events up through it.
                if (change.target == foldout)
                {
                    EditorState.instance.SetGroupClosed(key, !change.newValue);
                }
            });
        }

        private void ShowMatch()
        {
            if (!EditorApplication.isPlaying)
            {
                _match.text = "Enter Play mode to use the cheats. Layers switched on here draw once a match runs.";
                return;
            }

            if (!PlayWorld.TryGetAuthoritative(out var manager))
            {
                _match.text = "Playing, with no match running.";
                return;
            }

            var players = Count<Player>(manager);
            var units = Count<UnitTag>(manager);
            var buildings = Count<BuildingTag>(manager);
            _match.text = $"{FindRole(manager.World)}: {players} players, {units} units, {buildings} buildings.";
        }

        private static string FindRole(World world)
        {
            if (world.IsHost())
            {
                return "Host";
            }

            return world.IsServer() ? "Server" : "Single player";
        }

        private static int Count<T>(EntityManager manager) where T : IComponentData
        {
            using var query = manager.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }
    }
}
