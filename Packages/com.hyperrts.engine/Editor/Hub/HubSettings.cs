using HyperRTS.Editor.Common;
using UnityEditor;

namespace HyperRTS.Editor.Hub
{
    /// <summary>The Tools window's remembered page and Build tab.</summary>
    [FilePath("UserSettings/HyperRTSHub.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class HubSettings : UserSettings<HubSettings>
    {
        /// <summary>Full type name of the Tools window page shown last.</summary>
        public string toolsPage = "";

        /// <summary>Full type name of the Build page tab shown last.</summary>
        public string buildTab = "";
    }
}
