using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Simulation.Cues.Effects;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>Every surface effects asset has a valid catch-all rig and valid per-material rigs; an empty material rig plays nothing there.</summary>
    public sealed class SurfaceEffectsRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            foreach (var effects in assets.All<SurfaceEffects>())
            {
                EffectRigCheck.Check(effects, effects.anyMaterial, $"Surface effects '{effects.name}' anyMaterial", true, issues);
                foreach (var entry in effects.materials)
                {
                    EffectRigCheck.Check(effects, entry.rig, $"Surface effects '{effects.name}' {entry.material} rig", false, issues);
                }
            }
        }
    }
}
