using System.Collections.Generic;
using UnityEditor;

namespace HyperRTS.Editor.Common
{
    /// <summary>
    /// The remembered settings shared across editor tools; values that must reset each session stay in SessionState.
    /// </summary>
    [FilePath("UserSettings/HyperRTS.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class EditorState : UserSettings<EditorState>
    {
        /// <summary>Full type name of the Tools window page shown last.</summary>
        public string toolsPage = "";

        /// <summary>Full type name of the Build page tab shown last.</summary>
        public string buildTab = "";

        public bool validateSelectionOnly;

        /// <summary>Validate's severity chips: whether errors and warnings are listed.</summary>
        public bool validateErrors = true;

        public bool validateWarnings = true;

        public bool mapAutoPreview = true;

        /// <summary>Hierarchy rows of entity objects show their component icon, owner colour and issue dot.</summary>
        public bool hierarchyRowIcons = true;

        /// <summary>The Project grid shows a unit prefab's command card icon over its thumbnail.</summary>
        public bool projectUnitIcons = true;

        /// <summary>Project rows of a generator item's asset or folder show a dot when the item is out of date or failed.</summary>
        public bool projectGeneratorStates = true;

        /// <summary>Inspector header groups folded shut, as "Type/Header".</summary>
        public List<string> closedGroups = new();

        public bool IsGroupClosed(string group) => closedGroups.Contains(group);

        public void SetGroupClosed(string group, bool closed) => SetListed(closedGroups, group, closed);
    }
}
