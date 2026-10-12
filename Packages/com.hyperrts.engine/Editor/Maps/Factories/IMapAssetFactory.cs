using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// Makes the Unity assets of map library items whose file names it as their <c>factory</c>: a material, a terrain
    /// layer, a prop prefab. The game implements one per kind of asset it builds; the engine ships the generic ones.
    /// A factory reads its settings from the file's <c>params</c> object (<see cref="MapAssetRequest.Params{T}"/>) and
    /// writes its assets where the item's asset fields point, so adding an item means adding a JSON file.
    /// </summary>
    public interface IMapAssetFactory
    {
        /// <summary>The name library files give as their <c>factory</c>; unique across the project.</summary>
        string Kind { get; }

        /// <summary>Creates or updates the item's assets in place (keeping their GUIDs); returns the paths written.</summary>
        IReadOnlyList<string> Generate(MapAssetRequest request);
    }
}
