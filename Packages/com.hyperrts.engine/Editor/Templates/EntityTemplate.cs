using System;
using UnityEngine;

namespace HyperRTS.Editor.Templates;

/// <summary>One role template: its menu path (submenu and label) and the factory that builds it.</summary>
public sealed class EntityTemplate(string path, Func<GameObject> build)
{
    /// <summary>Menu path below the HyperRTS root, e.g. "Units/Combat Unit".</summary>
    public string Path { get; } = path;

    /// <summary>Last path segment; the created object is named after it.</summary>
    public string Label => Path[(Path.LastIndexOf('/') + 1)..];

    public GameObject Create() => Create(Label);

    /// <summary>Builds the template named <paramref name="name"/>.</summary>
    public GameObject Create(string name)
    {
        var go = build();
        EntityChoice.Rename(go, name);
        return go;
    }
}
