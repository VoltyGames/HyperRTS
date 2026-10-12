using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// A hidden object holding one library item and its refs while an editor shows it, so property fields, their
    /// drawers and Undo work on it. Never saved: the editor writes the JSON file itself.
    /// </summary>
    public sealed class MapLibraryItemWrapper : ScriptableObject
    {
        [SerializeReference]
        public object item;

        public MapLibraryRefs refs = new();
    }
}
