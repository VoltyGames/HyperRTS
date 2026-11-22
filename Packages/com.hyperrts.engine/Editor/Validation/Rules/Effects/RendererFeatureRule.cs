using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Project;
using HyperRTS.Editor.Validation.Sets;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>A match scene needs one renderer feature, with sane settings, on every active renderer.</summary>
    public abstract class RendererFeatureRule<TFeature> : ISceneRule where TFeature : ScriptableRendererFeature
    {
        protected abstract string FeatureName { get; }

        protected abstract string MissingMessage { get; }

        protected virtual bool MissingIsError => false;

        public void Check(SceneSet scenes, ValidationIssues issues)
        {
            if (!scenes.HostsSubScene)
            {
                return;
            }

            foreach (var renderer in RendererFeatures.ActiveRenderers())
            {
                CheckRenderer(renderer, issues);
            }
        }

        /// <summary>The problem with the feature's settings, or empty when they are sane.</summary>
        protected virtual string SettingsProblem(TFeature feature) => "";

        /// <summary>Extra checks on a renderer that has the feature.</summary>
        protected virtual void CheckRenderer(UniversalRendererData renderer, TFeature feature, ValidationIssues issues)
        {
            var problem = SettingsProblem(feature);
            if (problem.Length > 0)
            {
                issues.Error(renderer, problem);
            }
        }

        private void CheckRenderer(UniversalRendererData renderer, ValidationIssues issues)
        {
            var feature = RendererFeatures.Find<TFeature>(renderer);
            if (feature != null)
            {
                CheckRenderer(renderer, feature, issues);
                return;
            }

            void AddFeature() => RendererFeatures.Add(renderer, ScriptableObject.CreateInstance<TFeature>(), FeatureName);
            if (MissingIsError)
            {
                issues.Error(renderer, MissingMessage, "Add Feature", AddFeature);
                return;
            }

            issues.Warn(renderer, MissingMessage, "Add Feature", AddFeature);
        }
    }
}
