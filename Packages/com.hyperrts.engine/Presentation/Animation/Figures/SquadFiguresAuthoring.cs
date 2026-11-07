using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Draws a squad unit as bone-animated figures, one per Squad member. Each figure is a child whose Bone Animated
    /// renderers share one clip table, and its Figure Clip Set picks the clips it plays.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Squad Figures")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public partial class SquadFiguresAuthoring : AuthoringBehaviour
    {
        // Golden ratio steps spread any number of phases evenly over a loop.
        private const float PhaseStep = 0.618034f;

        /// <summary>A figure and the clip set it plays.</summary>
        [Serializable]
        public class Figure
        {
            [Tooltip("The figure, a child of this unit's model.")]
            public Transform root;

            [Tooltip("The clips this figure plays, as a rifleman and a launcher carrier stand differently.")]
            public FigureClipSet clips;

            [Header("Upgraded weapon")]
            [Tooltip("The figure shown instead while its member fires the upgraded weapon, a child of this unit's model. Empty keeps this one.")]
            public Transform upgradedRoot;

            [Tooltip("The clips the upgraded figure plays; required with an upgraded figure.")]
            public FigureClipSet upgradedClips;

            [Header("Emplaced")]
            [Tooltip("The figure shown instead while the unit's weapon is set up and its member takes a crew place, such as a gunner with empty hands. A child of this unit's model. Empty keeps this one. Not with an upgraded figure.")]
            public Transform emplacedRoot;

            [Tooltip("The clips the emplaced figure plays; required with an emplaced figure.")]
            public FigureClipSet emplacedClips;

            /// <summary>A figure shows at most one other model, so an upgraded and an emplaced one together are ambiguous.</summary>
            public bool HasTwoVariants => upgradedRoot != null && emplacedRoot != null;
        }

        [Tooltip("The deployed weapon's model, a child of this unit shown from when the unit starts deploying until it packs up. Empty for none.")]
        public Transform deployedModel;

        [Tooltip("The figures, one per Squad member in the same order.")]
        public List<Figure> figures = new();

        [Tooltip("Horizontal speed in m/s from which figures run instead of walk.")]
        [Min(0.3f)]
        public float runSpeed = 3f;

        [Tooltip("Seconds a dropped figure stays down before it hides.")]
        [Min(0f)]
        public float dieHold = 3f;

        public static void AddSquad(
            EntityWriter writer, in SquadFigures squad, IReadOnlyList<SquadFigure> figures)
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

        public SquadFigures Squad() => new() { RunSpeed = runSpeed, DieHold = dieHold };
    }
}
