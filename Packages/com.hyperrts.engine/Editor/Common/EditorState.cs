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
        /// <summary>Inspector header groups folded shut, as "Type/Header".</summary>
        public List<string> closedGroups = new();

        public bool IsGroupClosed(string group) => closedGroups.Contains(group);

        public void SetGroupClosed(string group, bool closed) => SetListed(closedGroups, group, closed);
    }
}
