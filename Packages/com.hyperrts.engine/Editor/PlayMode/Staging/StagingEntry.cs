using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.PlayMode.Staging.Steps;

namespace HyperRTS.Editor.PlayMode.Staging;

/// <summary>
/// One entry of a staging file's <c>steps</c>: its time and the one kind key it holds. To add a kind, give it a
/// field named after its key and list it in <see cref="Given"/>.
/// </summary>
internal sealed class StagingEntry
{
    public float at;
    public SpawnStep spawn;
    public OrderStep order;
    public CameraStep camera;
    public ViewStep view;
    public bool? clean;
    public string shot;
    public FramesStep frames;
    public FramesStep clip;
    public SampleStep sample;
    public string destroy;
    public DamageStep damage;
    public string ready;
    public float? speed;
    public bool? fog;
    public Dictionary<string, float> mixer;
    public DetailStep detail;
    public string quality;
    public string select;
    public string weather;
    public GroupStep group;

    /// <summary>The step of the one kind key this entry has, with the key; none or two throws.</summary>
    public (string Kind, StagingStep Step) Chosen()
    {
        var given = Given();
        var kind = StagingStep.OneOf(given.Select(pair => (pair.Key, pair.Step != null)).ToArray());
        return (kind, given.First(pair => pair.Key == kind).Step);
    }

    private List<(string Key, StagingStep Step)> Given() => new()
    {
        ("spawn", spawn),
        ("order", order),
        ("camera", camera),
        ("view", view),
        ("clean", clean is { } hide ? new CleanStep(hide) : null),
        ("shot", shot == null ? null : new ShotStep(shot)),
        ("frames", frames),
        ("clip", clip?.AsMovie()),
        ("sample", sample),
        ("destroy", destroy == null ? null : new DestroyStep(destroy)),
        ("damage", damage),
        ("ready", ready == null ? null : new ReadyStep(ready)),
        ("speed", speed is { } scale ? new SpeedStep(scale) : null),
        ("fog", fog is { } enabled ? new FogStep(enabled) : null),
        ("mixer", mixer == null ? null : new MixerStep(mixer)),
        ("detail", detail),
        ("quality", quality == null ? null : new QualityStep(quality)),
        ("select", select == null ? null : new SelectStep(select)),
        ("weather", weather == null ? null : new WeatherStep(weather)),
        ("group", group),
    };
}
