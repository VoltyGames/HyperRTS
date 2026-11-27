using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Effects.Preview;
using HyperRTS.Editor.Stages;

namespace HyperRTS.Editor.Effects.Stages;

/// <summary>
/// A stage the Effects page runs for a rig. The engine's are the graph build and the capture; a game adds its own,
/// such as making the rig's sheets, by subclassing, and every subclass is found.
/// </summary>
public abstract class EffectRigStage(string title) : Stage<EffectRigEntry>(title)
{
    public const string LogRoot = "Logs/Effects";

    /// <summary>Where the engine's stages sit in <see cref="Order"/>; a game's stage that makes what they read comes first.</summary>
    public const int GraphOrder = 200, CaptureOrder = 300;

    /// <summary>The stages that apply to <paramref name="rig"/>, in the order they run.</summary>
    public static List<EffectRigStage> For(EffectRigEntry rig) =>
        TypeDiscovery.Instances<EffectRigStage>().Where(stage => stage.Applies(rig)).OrderBy(stage => stage.Order).ToList();

    /// <summary>Where the stage sits in a rig's list: the order the page runs them in.</summary>
    public abstract int Order { get; }

    public abstract bool Applies(EffectRigEntry rig);

    /// <summary>
    /// Runs <paramref name="work"/>, which ends within the Editor, and writes the lines it returns to the log, or its
    /// error with exit code 1 before throwing it again.
    /// </summary>
    protected async Task Record(EffectRigEntry rig, Func<Task<string>> work)
    {
        var log = Log(rig);
        try
        {
            log.Write(await work(), 0);
        }
        catch (Exception exception)
        {
            log.Write(exception.Message, 1);
            throw;
        }
    }

    /// <summary>The stage's log for <paramref name="rig"/>, under <see cref="LogRoot"/>.</summary>
    public override StageLogFile Log(EffectRigEntry rig) => new($"{LogRoot}/{rig.Name}/{Title.ToLowerInvariant()}.log");
}
