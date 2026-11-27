using HyperRTS.Presentation.Environment.Weather;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Switches the map to another of its weathers; an unknown key fails naming the keys the set has.</summary>
internal sealed class WeatherStep : StagingStep
{
    private readonly string _key;

    public WeatherStep(string key) => _key = key;

    public override void Run(StagingRun run)
    {
        var set = Object.FindAnyObjectByType<WeatherSet>();
        if (set == null)
        {
            throw new System.InvalidOperationException("the scene has no WeatherSet");
        }

        set.Apply(_key);
    }
}
