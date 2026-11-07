using System.Collections.Generic;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.PlayMode;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// The editor tools' remembered settings, per user and project in <c>UserSettings/HyperRTS.asset</c>. Layouts bind
    /// their fields to it with <c>binding-path</c>; values that must reset each session stay in SessionState.
    /// </summary>
    [FilePath("UserSettings/HyperRTS.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class EditorState : ScriptableSingleton<EditorState>
    {
        /// <summary>Full type name of the Tools window page shown last.</summary>
        public string toolsPage = "";

        /// <summary>Full type name of the Build page tab shown last.</summary>
        public string buildTab = "";

        public bool validateSelectionOnly;

        public bool mapAutoPreview = true;

        /// <summary>Full type names of the debug layers switched on.</summary>
        public List<string> debugLayers = new();

        /// <summary>Inspector header groups folded shut, as "Type/Header".</summary>
        public List<string> closedGroups = new();

        [Min(0)]
        public int cheatResources = 1000;

        [Min(1)]
        public int armiesPerSide = 250;

        public BotMatches.Settings bots = new();

        public EffectPreviewSettings effectPreview = new();

        /// <summary>Binds the <c>binding-path</c> fields under <paramref name="root"/> to the state and saves each change.</summary>
        public static void Bind(VisualElement root)
        {
            var serialized = new SerializedObject(instance);
            root.Bind(serialized);
            root.TrackSerializedObjectValue(serialized, _ => instance.Save());
        }

        public void Save() => Save(true);

        public bool IsGroupClosed(string group) => closedGroups.Contains(group);

        public void SetGroupClosed(string group, bool closed)
        {
            closedGroups.Remove(group);
            if (closed)
            {
                closedGroups.Add(group);
            }

            Save();
        }

        public bool IsLayerOn(string layer) => debugLayers.Contains(layer);

        public void SetLayer(string layer, bool on)
        {
            debugLayers.Remove(layer);
            if (on)
            {
                debugLayers.Add(layer);
            }

            Save();
        }
    }
}
