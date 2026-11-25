using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// A game's extra step of the New Entity dialog: its own fields, and what it makes beside the saved prefab.
    /// </summary>
    public abstract class EntityWizardStep
    {
        /// <summary>The steps that apply to a prefab saved in <paramref name="folder"/>.</summary>
        public static List<EntityWizardStep> For(string folder) =>
            TypeDiscovery.Instances<EntityWizardStep>().Where(step => step.Applies(folder)).ToList();

        /// <summary>Whether the step runs for a prefab saved in <paramref name="folder"/>.</summary>
        public abstract bool Applies(string folder);

        /// <summary>The step's fields; call <paramref name="changed"/> when a value changes so Create is checked again.</summary>
        public abstract VisualElement CreateForm(Action changed);

        /// <summary>Why the form can't run for a prefab called <paramref name="name"/>, or null when it can.</summary>
        public abstract string Problem(string name);

        /// <summary>Runs after the prefab asset is saved, with the form's values as they stand.</summary>
        public abstract void Run(GameObject prefab);

        /// <summary>The UXML named after this step's type plus <paramref name="part"/>, beside its script.</summary>
        protected VisualTreeAsset Layout(string part = "") => EditorAssets.Layout(GetType(), part);
    }
}
