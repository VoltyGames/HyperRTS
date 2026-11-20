using HyperRTS.Editor.PlayMode.Staging.Steps;
using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// One timed step of a <see cref="StagingFile"/>. To add a kind, subclass it with a static <c>Read</c>, and list the
    /// kind's key in <see cref="Readers"/>.
    /// </summary>
    internal abstract class StagingStep
    {
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, Func<StagingJson, StagingStep>> Readers = new()
        {
            ["spawn"] = SpawnStep.Read,
            ["order"] = OrderStep.Read,
            ["camera"] = CameraStep.Read,
            ["view"] = ViewStep.Read,
            ["clean"] = CleanStep.Read,
            ["shot"] = ShotStep.Read,
            ["frames"] = FramesStep.Read,
            ["clip"] = FramesStep.ReadClip,
            ["sample"] = SampleStep.Read,
            ["destroy"] = DestroyStep.Read,
            ["damage"] = DamageStep.Read,
            ["ready"] = ReadyStep.Read,
            ["speed"] = SpeedStep.Read,
            ["fog"] = FogStep.Read,
            ["mixer"] = MixerStep.Read,
            ["detail"] = DetailStep.Read,
        };

        /// <summary>Position in the file, for messages.</summary>
        public int Index { get; private set; }

        /// <summary>Seconds of game time after the match is live.</summary>
        public float At { get; private set; }

        /// <summary>The step's key in the file, such as "spawn".</summary>
        public string Kind { get; private set; }

        /// <summary>The group label this step makes, or null.</summary>
        public virtual string Defines => null;

        /// <summary>Group labels this step needs to exist already.</summary>
        public virtual IEnumerable<string> Uses => Array.Empty<string>();

        /// <summary>The file or folder this step writes, so two steps never share one; null when it writes none.</summary>
        public virtual string Output => null;

        /// <summary>Later steps are worthless without this one, so its error ends the run at once.</summary>
        public virtual bool EndsRunOnError => false;

        public abstract void Run(StagingRun run);

        public static StagingStep Read(StagingJson json, int index)
        {
            var at = json.Number("at");
            if (at < 0f)
            {
                throw json.Problem($"has a negative 'at' of {at}");
            }

            var kind = json.Choose(Readers.Keys);
            var step = Readers[kind](json);
            step.Kind = kind;
            step.Index = index;
            step.At = at;
            return step;
        }
    }
}
