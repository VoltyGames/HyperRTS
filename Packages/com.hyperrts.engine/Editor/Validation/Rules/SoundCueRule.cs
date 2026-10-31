using System.Linq;
using HyperRTS.Simulation.Cues;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A sound cue plays one of its clips at random, so it needs at least one and none missing.</summary>
    public sealed class SoundCueRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            foreach (var cue in assets.All<SoundCue>())
            {
                if (cue.clips.Length == 0)
                {
                    issues.Error(cue, $"Sound cue '{cue.name}' has no clips, so it plays nothing.");
                }
                else if (cue.clips.Any(clip => clip == null))
                {
                    issues.Error(cue, $"Sound cue '{cue.name}' has a missing clip; remove the entry or set it.");
                }
            }
        }
    }
}
