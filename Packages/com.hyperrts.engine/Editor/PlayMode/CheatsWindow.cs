using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.PlayMode.Cheats;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Entities;
using Unity.Netcode;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Cheats: the match at a glance, then the player, spawning, combat, strikes and world cheats.</summary>
    public class CheatsWindow : PlayModeWindow
    {
        private const long PollMilliseconds = 500;

        private Label _match;

        [MenuItem(EditorMenu.Cheats, false, EditorMenu.CheatsPriority)]
        public static void Open() => Open<CheatsWindow>("Cheats");

        protected override void Build(VisualElement root)
        {
            new CheatsSection(root.Require<VisualElement>("cheats"));
            _match = root.Require<Label>("match");
            ShowMatch();
            root.schedule.Execute(ShowMatch).Every(PollMilliseconds);
        }

        private void ShowMatch()
        {
            if (!EditorApplication.isPlaying)
            {
                _match.text = "Enter Play mode to use the cheats.";
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
            _match.text = $"{FindRole(manager.World)}: {players} players, {units} units, {buildings} buildings";
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
