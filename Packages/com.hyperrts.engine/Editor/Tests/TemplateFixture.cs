using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests;

/// <summary>Destroys the objects a test tracked once it finishes.</summary>
public abstract class TemplateFixture
{
    private readonly List<Object> _created = new();

    [TearDown]
    public void DestroyTracked()
    {
        foreach (var created in _created)
        {
            if (created != null)
            {
                Object.DestroyImmediate(created);
            }
        }

        _created.Clear();
    }

    public T Track<T>(T created) where T : Object
    {
        _created.Add(created);
        return created;
    }

    public GameObject Make(string name) => Track(new GameObject(name));

    public T Add<T>(GameObject go) where T : Component => Track(go).AddComponent<T>();

    /// <summary>Tracks a template and returns its <typeparamref name="T"/>, which the template must carry.</summary>
    public T Template<T>(GameObject template) where T : Component => Track(template).GetComponent<T>();
}
