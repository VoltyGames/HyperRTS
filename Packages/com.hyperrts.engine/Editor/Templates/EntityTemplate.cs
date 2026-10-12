using System;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
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

        /// <summary>Builds the template named <paramref name="name"/>, which is also its HUD display name.</summary>
        public GameObject Create(string name)
        {
            var go = _build();
            go.name = name;
            if (go.TryGetComponent(out GameEntityAuthoring entity))
            {
                entity.displayName = name;
            }

            if (go.TryGetComponent(out UpgradeAuthoring upgrade))
            {
                upgrade.displayName = name;
            }

            return go;
        }
    }
}
