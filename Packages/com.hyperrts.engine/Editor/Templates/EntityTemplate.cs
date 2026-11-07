using System;
using HyperRTS.Simulation.Production;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>One role template: its menu path (submenu and label) and the factory that builds it.</summary>
    public sealed class EntityTemplate
    {
        private readonly Func<GameObject> _build;

        public EntityTemplate(string path, Func<GameObject> build)
        {
            Path = path;
            _build = build;
        }

        /// <summary>Menu path below the HyperRTS root, e.g. "Units/Combat Unit".</summary>
        public string Path { get; }

        /// <summary>Last path segment; the created object is named after it.</summary>
        public string Label => Path.Substring(Path.LastIndexOf('/') + 1);

        public GameObject Create() => Create(Label);

        /// <summary>Builds the template named <paramref name="name"/>.</summary>
        public GameObject Create(string name)
        {
            var go = _build();
            Rename(go, name);
            return go;
        }

        /// <summary>
        /// Names the object and its HUD display name, which is also its type identity; underscores read as spaces, so
        /// <c>USA_M1A2</c> shows as "USA M1A2".
        /// </summary>
        public static void Rename(GameObject go, string name)
        {
            go.name = name;
            if (go.TryGetComponent(out ProducibleAuthoring producible))
            {
                producible.displayName = name.Replace('_', ' ');
            }
        }
    }
}
