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

        [Tooltip("Clip in the figures' table played standing still.")]
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

        /// <summary>The figures' shared clip table; fails when a figure is missing or uses another.</summary>
        public VATClipTable Table()
        {
            if (figures.Count == 0)
            {
                throw new InvalidOperationException($"Squad Figures on '{name}' lists no figures.");
            }

            var table = figures[0] != null ? figures[0].clips : null;
            foreach (var figure in figures)
            {
                if (figure == null || figure.clips != table)
                {
                    throw new InvalidOperationException(
                        $"Squad Figures on '{name}' needs every figure set and sharing one clip table.");
                }
            }

            return table;
        }

        public SquadFigures Squad(VATClipTable table) => new()
        {
            Idle = table.Range(idleClip),
            Walk = table.Range(walkClip),
            Run = table.Range(runClip),
            Fire = table.Range(fireClip),
            Die = table.Range(dieClip),
            RunSpeed = runSpeed,
            DieHold = dieHold,
        };

        public class Baker : Baker<SquadFiguresAuthoring>
        {
            public override void Bake(SquadFiguresAuthoring authoring)
            {
                var table = authoring.Table();
                DependsOn(table);
                var entities = new List<SquadFigure>();
                foreach (var figure in authoring.figures)
                {
                    var entity = GetEntity(figure, TransformUsageFlags.Dynamic);
                    entities.Add(new SquadFigure { Figure = entity, Scale = figure.transform.localScale.x });
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AddSquad(ref writer, authoring.Squad(table), entities);
            }
        }
    }
}
