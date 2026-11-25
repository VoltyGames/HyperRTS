using HyperRTS.Editor.Common;
using UnityEditor;

namespace HyperRTS.Editor.Maps.Designer.Window
{
    /// <summary>The Map Editor's remembered controls.</summary>
    [FilePath("UserSettings/HyperRTSMapDesigner.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class MapDesignerSettings : UserSettings<MapDesignerSettings>
    {
        public bool autoPreview = true;
    }
}
