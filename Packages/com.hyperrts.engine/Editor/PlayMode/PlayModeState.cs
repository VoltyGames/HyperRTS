using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.PlayMode.BotMatches;
using HyperRTS.Editor.PlayMode.Launch;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode;

/// <summary>The Play page's, cheats' and debug layers' remembered settings.</summary>
[FilePath("UserSettings/HyperRTSPlayMode.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class PlayModeState : UserSettings<PlayModeState>
{
    /// <summary>Full type names of the debug layers switched on.</summary>
    public List<string> debugLayers = new();

    [Min(0)]
    public int cheatResources = 1000;

    [Min(1)]
    public int armiesPerSide = 250;

    /// <summary>The cheats' spawn choice, as an <c>EntityInfo.TypeId</c>.</summary>
    public int spawnTypeId;

    [Min(1)]
    public int spawnCount = 1;

    public LaunchChoice launch = new();

    public BotMatchSettings bots = new();

    public bool IsLayerShown(string layer) => debugLayers.Contains(layer);

    public void ShowLayer(string layer, bool shown) => SetListed(debugLayers, layer, shown);

    public void HideLayers()
    {
        debugLayers.Clear();
        Save();
    }
}
