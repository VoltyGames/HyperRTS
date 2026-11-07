using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>One row of the New Entity wizard: a heading, an engine template or a project's base prefab.</summary>
    public sealed class EntityChoice
    {
        private readonly Func<string, GameObject> _build;

        private EntityChoice(string label, Texture icon, Func<string, GameObject> build)
        {
            Label = label;
            Icon = icon;
            _build = build;
        }

        public string Label { get; }

        public Texture Icon { get; }

        public bool IsHeading => _build == null;

        /// <summary>Headings with their choices: every project's base groups first, then the engine templates.</summary>
        public static IEnumerable<(EntityChoice Heading, List<EntityChoice> Choices)> All()
        {
            foreach (var bases in EditorAssets.FindAssets<EntityBases>())
            {
                foreach (var group in bases.groups)
                {
                    yield return (Heading(group.name), group.prefabs.Select(Variant).ToList());
                }
            }

            yield return (Heading("Templates"), TemplateRegistry.All.Select(Template).ToList());
        }

        /// <summary>Builds the entity named <paramref name="name"/> as a scene object, ready to save as a prefab.</summary>
        public GameObject Build(string name) => _build(name);

        private static EntityChoice Heading(string label) => new(label, null, null);

        private static EntityChoice Template(EntityTemplate template) =>
            new(template.Path.Replace("/", " / "), null, template.Create);

        // Saving a prefab instance as a new asset makes a variant of it.
        private static EntityChoice Variant(GameObject prefab)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException("An EntityBases group lists an empty prefab slot.");
            }

            return new EntityChoice(prefab.name, AssetPreview.GetMiniThumbnail(prefab), name =>
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                EntityTemplate.Rename(instance, name);
                return instance;
            });
        }
    }
}
