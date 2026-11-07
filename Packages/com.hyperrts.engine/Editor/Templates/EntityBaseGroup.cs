using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>One heading of <see cref="EntityBases"/>, such as "Units", with its base prefabs.</summary>
    [Serializable]
    public sealed class EntityBaseGroup
    {
        [Tooltip("Heading in the New Entity wizard.")]
        public string name;

        [Tooltip("Base prefabs a new entity can be a variant of.")]
        public List<GameObject> prefabs = new();
    }
}
