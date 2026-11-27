using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Stages;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Stages;

/// <summary>
/// Captures a rig at the default times and camera, as <c>effect capture</c> does without a label; its images are its
/// sheet, and it is out of date once the rig or anything it draws with changes.
/// </summary>
public sealed class EffectCaptureStage : EffectRigStage
{
    public EffectCaptureStage()
        : base("Capture")
    {
    }

    public override int Order => CaptureOrder;

    public override bool Applies(EffectRigEntry rig) => true;

    public override StageState State(EffectRigEntry rig) => new StageFiles(Log(rig), Inputs(rig), Sheets(rig)).State();

    /// <summary>The images the last good capture wrote, which its log lists.</summary>
    public override IReadOnlyList<string> Sheets(EffectRigEntry rig)
    {
        var log = Log(rig);
        if (!log.Exists || log.Failed)
        {
            return Array.Empty<string>();
        }

        return log.Text().Split('\n').Select(line => line.Trim())
            .Where(line => line.EndsWith(".png", StringComparison.Ordinal))
            .ToList();
    }

    public override Task Run(EffectRigEntry rig) => Record(rig, async () =>
    {
        var files = await new EffectCapture(rig.Path, new EffectCaptureSettings()).Render();
        return string.Join("\n", files);
    });

    // Scripts are left out: an engine edit changes no picture.
    private static IEnumerable<string> Inputs(EffectRigEntry rig) => AssetDatabase.GetDependencies(rig.Path, true)
        .Where(path => Path.GetExtension(path) != ".cs")
        .Concat(rig.Recipes);
}
