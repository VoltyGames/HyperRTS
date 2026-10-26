using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
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

        protected T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
