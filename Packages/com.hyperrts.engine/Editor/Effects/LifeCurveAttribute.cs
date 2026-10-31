using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>Draws a list of <see cref="CurveKey"/> over a particle's life as one curve in the inspector.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LifeCurveAttribute : PropertyAttribute
    {
        public LifeCurveAttribute()
            : base(true)
        {
        }
    }
}
