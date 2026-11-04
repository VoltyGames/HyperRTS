using HyperRTS.Simulation.Common.Authoring;
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
    /// Draws a squad unit as bone-animated figures, one per Squad member in order: a dead member's figure dies then
    /// hides, the rest idle, walk, run, crouch or crawl as their own soldier does, with the upper body firing over the
    /// legs. A figure is a hand-placed child whose Bone Animated renderers (body levels of detail, weapon) share one
    /// clip table.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Squad Figures")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public partial class SquadFiguresAuthoring : AuthoringBehaviour
    {
        // Golden ratio steps spread any number of phases evenly over a loop.
        private const float PhaseStep = 0.618034f;

        /// <summary>A figure and which of the bake's stance clips it plays.</summary>
        [Serializable]
        public class Figure
        {
            [Tooltip("The figure, a child of this unit's model.")]
            public Transform root;

            [Tooltip("Added before this squad's idle, walk, run, fire, crouch and prone clip names, as a rifleman and a launcher carrier stand differently. Empty for none.")]
            public string clipPrefix;

            [Tooltip("The weapon's designation, shown on the squad's member card (M4A1, PKP). Empty for none.")]
            public string weaponName;

            [Header("Upgraded weapon")]
            [Tooltip("The figure shown instead while its member fires the upgraded weapon, a child of this unit's model. Empty keeps this one.")]
            public Transform upgradedRoot;

            [Tooltip("Clip prefix of the upgraded figure, as above.")]
            public string upgradedClipPrefix;

            [Tooltip("The upgraded weapon's designation for the member card.")]
            public string upgradedWeaponName;

            [Header("Emplaced")]
            [Tooltip("The figure shown instead while the unit's weapon is set up and this figure takes a crew place, such as a gunner with empty hands. A child of this unit's model. Empty keeps this one. Not with an upgraded figure.")]
            public Transform emplacedRoot;

            [Tooltip("Clip prefix of the emplaced figure, as above.")]
            public string emplacedClipPrefix;

            [Tooltip("The emplaced figure's weapon designation for the member card.")]
            public string emplacedWeaponName;

            [Header("Deployed")]
            [Tooltip("Stands at the crew place below while the unit's weapon is set up, instead of its slot in the squad.")]
            public bool takesCrewPlace;

            [Tooltip("Metres in the unit's frame, x to the right and y ahead, such as 0.6 behind the gun.")]
            public Vector2 crewOffset;

            [Tooltip("How it holds itself at the crew place. Fire and idle use the posture's clips.")]
            public Posture crewPosture = Posture.Prone;

            /// <summary>A figure shows at most one other model, so an upgraded and an emplaced one together are ambiguous.</summary>
            public bool HasTwoVariants => upgradedRoot != null && emplacedRoot != null;
        }

        [Tooltip("The deployed weapon's model, a child of this unit shown from when the unit starts deploying until it packs up. Empty for none.")]
        public Transform deployedModel;

        [Tooltip("The figures, one per Squad member in the same order.")]
        public List<Figure> figures = new();

        [Tooltip("Clip played standing still.")]
        public string idleClip = "Idle";

        [Tooltip("Clip played moving slower than the run speed.")]
        public string walkClip = "Walk";

        [Tooltip("Clip played moving at or above the run speed.")]
        public string runClip = "Run";

        [Tooltip("Clip the upper body plays while the primary weapon is firing.")]
        public string fireClip = "Fire";

        [Tooltip("Crouched and still. Empty plays the standing idle clip.")]
        public string crouchIdleClip;

        [Tooltip("Crouched and moving. Empty plays the standing walk clip.")]
        public string crouchWalkClip;

        [Tooltip("Upper body firing while crouched. Empty plays the standing fire clip.")]
        public string crouchFireClip;

        [Tooltip("Prone and still. Empty plays the standing idle clip.")]
        public string proneIdleClip;

        [Tooltip("Prone and moving. Empty plays the standing walk clip.")]
        public string proneCrawlClip;

        [Tooltip("Upper body firing while prone. Empty plays the standing fire clip.")]
        public string proneFireClip;

        [Tooltip("Clip played, whole body, while digging at a field work. Empty plays the standing idle clip.")]
        public string digClip = "Dig";

        [Tooltip("Clip a figure plays once when health drops it.")]
        public string dieClip = "Die";

        [Tooltip("Clip played once instead of Die when a blast ahead of the figure drops it. Empty for none.")]
        public string dieBlastFrontClip;

        [Tooltip("Clip played instead of Die when a blast behind the figure drops it. Empty for none.")]
        public string dieBlastBackClip;

        [Tooltip("Clip played instead of Die when a blast on the figure's left drops it. Empty for none.")]
        public string dieBlastLeftClip;

        [Tooltip("Clip played instead of Die when a blast on the figure's right drops it. Empty for none.")]
        public string dieBlastRightClip;

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
