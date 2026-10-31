using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>Draws a recipe's asset path as an object picker of <see cref="Type"/>; the recipe keeps the path.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EffectAssetAttribute : PropertyAttribute
    {
        public EffectAssetAttribute(Type type) => Type = type;

        public Type Type { get; }
    }
}
