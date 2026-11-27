using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Simulation.Cues.Sounds;

namespace HyperRTS.Editor.Validation.Rules.Fields;

/// <summary>
/// A sound cue plays one of its clips at random, so it needs at least one and none missing; a 3D cue also needs a
/// distance range its fade can run over.
/// </summary>
public sealed class SoundCueRule : IAssetRule
{
    public void Check(AssetSet assets, ValidationIssues issues)
    {
        foreach (var cue in assets.All<SoundCue>())
        {
            CheckClips(cue, issues);
            if (cue.spatial)
            {
                CheckDistances(cue, issues);
            }
        }
    }

    private static void CheckClips(SoundCue cue, ValidationIssues issues)
    {
        if (cue.clips.Length == 0)
        {
            issues.Error(cue, $"Sound cue '{cue.name}' has no clips, so it plays nothing.");
        }

        if (cue.clips.Any(clip => clip == null) || cue.distantClips.Any(clip => clip == null))
        {
            issues.Error(cue, $"Sound cue '{cue.name}' has a missing clip; remove the entry or set it.");
        }
    }

    private static void CheckDistances(SoundCue cue, ValidationIssues issues)
    {
        // The fade runs inversely from the minimum distance, so a zero minimum would silence the cue.
        if (cue.minDistance <= 0f || cue.maxDistance <= cue.minDistance)
        {
            issues.Error(cue, $"Sound cue '{cue.name}' needs 0 < Min Distance < Max Distance to fade over.");
        }

        if (cue.distantClips.Length > 0 && cue.distantFrom >= cue.maxDistance)
        {
            issues.Error(cue, $"Sound cue '{cue.name}' starts its distant clips beyond Max Distance, so they never play.");
        }
    }
}
