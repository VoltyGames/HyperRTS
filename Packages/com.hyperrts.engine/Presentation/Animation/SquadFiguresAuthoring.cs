using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Draws a squad unit as its VAT figures: one per share of health, the lost ones dying then hiding, the rest
    /// idling, walking, running or firing with the unit. The figures are hand-placed children with VAT Animated.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Squad Figures")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class SquadFiguresAuthoring : AuthoringBehaviour
    {
        // Golden ratio steps spread any number of phases evenly over a loop.
        private const float PhaseStep = 0.618034f;

        [Tooltip("The figures, children of this unit's model, in the order they stay: the last one falls first.")]
        public List<VATAnimatedAuthoring> figures = new();

        [Tooltip("Clip played standing still, looked up in each figure's own table.")]
        public string idleClip = "Idle";

        [Tooltip("Clip played moving slower than the run speed.")]
        public string walkClip = "Walk";

        [Tooltip("Clip played moving at or above the run speed.")]
        public string runClip = "Run";

        [Tooltip("Clip played while the primary weapon is firing.")]
        public string fireClip = "Fire";

        [Tooltip("Clip a figure plays once when health drops it.")]
        public string dieClip = "Die";

        [Tooltip("Horizontal speed in m/s from which figures run instead of walk.")]
        [Min(0.3f)]
        public float runSpeed = 3f;

        [Tooltip("Seconds a dropped figure stays down before it hides.")]
        [Min(0f)]
        public float dieHold = 3f;

        public static void AddSquad<TWriter>(
            ref TWriter writer, in SquadFigures squad, IReadOnlyList<SquadFigure> figures) where TWriter : struct, IEntityWriter
        {
            writer.Add(squad);
            var buffer = writer.AddBuffer<SquadFigure>();
            for (var i = 0; i < figures.Count; i++)
            {
                var figure = figures[i];
                figure.Phase = math.frac(i * PhaseStep);
                buffer.Add(figure);
            }
        }

        /// <summary>
        /// Each figure's clips from its own table by this squad's clip names; fails when a figure, its table or one
        /// of the five clips is missing.
        /// </summary>
        public FigureClips[] Clips()
        {
            if (figures.Count == 0)
            {
                throw new InvalidOperationException($"Squad Figures on '{name}' lists no figures.");
            }

            var clips = new FigureClips[figures.Count];
            for (var i = 0; i < figures.Count; i++)
            {
                var figure = figures[i];
                if (figure == null || figure.clips == null)
                {
                    throw new InvalidOperationException($"Squad Figures on '{name}' needs every figure set with a clip table.");
                }

                clips[i] = Clips(figure.clips);
            }

            return clips;
        }

        public SquadFigures Squad() => new() { RunSpeed = runSpeed, DieHold = dieHold };

        private FigureClips Clips(VATClipTable table) => new()
        {
            Idle = table.Range(idleClip),
            Walk = table.Range(walkClip),
            Run = table.Range(runClip),
            Fire = table.Range(fireClip),
            Die = table.Range(dieClip),
        };

        public class Baker : Baker<SquadFiguresAuthoring>
        {
            public override void Bake(SquadFiguresAuthoring authoring)
            {
                var clips = authoring.Clips();
                var entities = new List<SquadFigure>();
                for (var i = 0; i < clips.Length; i++)
                {
                    var figure = authoring.figures[i];
                    DependsOn(figure.clips);
                    var entity = GetEntity(figure, TransformUsageFlags.Dynamic);
                    entities.Add(new SquadFigure { Figure = entity, Clips = clips[i] });
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AddSquad(ref writer, authoring.Squad(), entities);
            }
        }
    }
}
