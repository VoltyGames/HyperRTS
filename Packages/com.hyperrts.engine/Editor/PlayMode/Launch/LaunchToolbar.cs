using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>The main toolbar's launch choice beside Play: its label, a menu to start it and a way into the launcher.</summary>
    internal static class LaunchToolbar
    {
        public const string Path = "HyperRTS/Launch";

        [MainToolbarElement(Path, defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static MainToolbarElement Create()
        {
            var game = GameLauncher.Find();
            var choice = EditorState.instance.launch;
            var label = "Launch";
            if (game != null && game.TryFindMap(choice.map, out var map))
            {
                label = choice.Label(map.Name);
            }

            var content = new MainToolbarContent(label, "Start a match from the HyperRTS launcher");
            return new MainToolbarDropdown(content, area => ShowMenu(game, choice, area)) { displayed = game != null };
        }

        private static void ShowMenu(GameLauncher game, LaunchChoice choice, Rect area)
        {
            var menu = new GenericMenu();
            var problem = choice.Problem(game);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                menu.AddDisabledItem(new GUIContent("Play (leave Play mode first)"));
            }
            else if (choice.entry == LaunchEntry.Map && problem.Length > 0)
            {
                menu.AddDisabledItem(new GUIContent($"Play ({problem})"));
            }
            else
            {
                menu.AddItem(new GUIContent("Play"), false, () => game.Play(choice));
            }

            menu.AddItem(new GUIContent("Launcher..."), false, ToolsWindow.Show<PlayPage>);
            menu.DropDown(area);
        }
    }
}
