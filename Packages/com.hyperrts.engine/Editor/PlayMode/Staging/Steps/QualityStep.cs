using System;
using HyperRTS.Presentation.Quality;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>
/// Switches to a quality level by name with the game's settings for it, so later shots show what that level
/// draws. The run puts the level and the settings back when it ends.
/// </summary>
internal sealed class QualityStep(string name) : StagingStep
{
    private readonly string _name = name;

    public override void Check() => Level();

    public override void Run(StagingRun run)
    {
        var (level, settings) = (QualitySettings.GetQualityLevel(), PresentationQuality.Current);
        PlayQuality.Set(Level());
        run.Undo(() =>
        {
            QualitySettings.SetQualityLevel(level, true);
            PresentationQuality.Apply(settings);
        });
    }

    private int Level()
    {
        var level = Array.IndexOf(QualitySettings.names, _name);
        return level >= 0 ? level : throw new InvalidOperationException(
            $"has a 'quality' of '{_name}', not one of {string.Join(", ", QualitySettings.names)}");
    }
}
