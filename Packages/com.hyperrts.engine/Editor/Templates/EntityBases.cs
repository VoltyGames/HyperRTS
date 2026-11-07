using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>A project's base prefabs by group, which the New Entity wizard offers as prefab variants.</summary>
    [CreateAssetMenu(menuName = "HyperRTS/Entity Bases", fileName = "EntityBases")]
    public sealed class EntityBases : ScriptableObject
    {
        [Tooltip("Each group's heading and base prefabs, in the order the New Entity wizard lists them.")]
        public List<EntityBaseGroup> groups = new();
    }
}
