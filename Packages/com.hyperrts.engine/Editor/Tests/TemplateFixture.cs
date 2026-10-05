using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Destroys the GameObjects a test tracked once it finishes.</summary>
    public abstract class TemplateFixture
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void DestroyTracked()
        {
            _created.ForEach(Object.DestroyImmediate);
            _created.Clear();
        }

        protected GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }
    }
}
