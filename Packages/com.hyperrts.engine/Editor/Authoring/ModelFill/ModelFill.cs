using System;
using UnityEngine;

namespace HyperRTS.Editor.Authoring.ModelFill;

/// <summary>Sets an authoring component's values from the model under it. Derive from <see cref="ModelFill{T}"/>.</summary>
public abstract class ModelFill
{
    public abstract bool AppliesTo(Type type);

    /// <summary>Throws <see cref="InvalidOperationException"/> naming what the model lacks.</summary>
    public abstract void Fill(Component component);
}

/// <summary>
/// The fill from model of <typeparamref name="T"/>. Subclass it (engine or game) and the component gets a Fill from
/// model button, the Project window command and the rule that reports values off from the model.
/// </summary>
public abstract class ModelFill<T> : ModelFill where T : Component
{
    public override bool AppliesTo(Type type) => typeof(T).IsAssignableFrom(type);

    public sealed override void Fill(Component component) => Fill((T)component);

    /// <summary>Sets the values from the model alone, the same each run, and throws naming a part it lacks.</summary>
    protected abstract void Fill(T component);
}
