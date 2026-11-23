using System;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Sets an authoring component's values from the model under it. Derive from <see cref="ModelFill{T}"/>.</summary>
    public interface IModelFill
    {
        bool AppliesTo(Type type);

        /// <summary>Throws <see cref="InvalidOperationException"/> naming what the model lacks.</summary>
        void Fill(Component component);
    }
}
