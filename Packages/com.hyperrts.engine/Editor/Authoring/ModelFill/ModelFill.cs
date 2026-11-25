using System;
using UnityEngine;

namespace HyperRTS.Editor.Authoring.ModelFill
{
    /// <summary>
    /// The fill from model of <typeparamref name="T"/>. Subclass it (engine or game) and the component gets a Fill from
    /// model button, the Project window command and the rule that reports values off from the model.
    /// </summary>
    public abstract class ModelFill<T> : IModelFill where T : Component
    {
        public bool AppliesTo(Type type) => typeof(T).IsAssignableFrom(type);

        void IModelFill.Fill(Component component) => Fill((T)component);

        /// <summary>Sets the values from the model alone, the same each run, and throws naming a part it lacks.</summary>
        protected abstract void Fill(T component);
    }
}
