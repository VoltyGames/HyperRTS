using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Decorations;

/// <summary>What the Hierarchy and Project row decorations share: the dot colours and the Project list row height.</summary>
internal static class DecorationStyle
{
    /// <summary>Project window rows at or under this height are list view, taller ones grid view.</summary>
    public const float ListRowHeight = 20f;

    [NoAutoStaticsCleanup]
    private static readonly Color WarningColor = new Color32(196, 136, 0, 255);

    [NoAutoStaticsCleanup]
    private static readonly Color ErrorColor = new Color32(222, 64, 52, 255);

    public static Color DotColor(MessageType severity) => severity == MessageType.Error ? ErrorColor : WarningColor;
}
