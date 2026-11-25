using HyperRTS.Editor.Common;
using UnityEditor;

namespace HyperRTS.Editor.Decorations
{
    /// <summary>Which Hierarchy and Project window row decorations are shown.</summary>
    [FilePath("UserSettings/HyperRTSDecorations.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class DecorationSettings : UserSettings<DecorationSettings>
    {
        /// <summary>Hierarchy rows of entity objects show their component icon, owner colour and issue dot.</summary>
        public bool hierarchyRowIcons = true;

        /// <summary>The Project grid shows a unit prefab's command card icon over its thumbnail.</summary>
        public bool projectUnitIcons = true;

        /// <summary>Project rows of a generator item's asset or folder show a dot when the item is out of date or failed.</summary>
        public bool projectGeneratorStates = true;
    }
}
