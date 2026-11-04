using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using HyperRTS.Core;
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
    public class SquadFiguresAuthoring : AuthoringBehaviour
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
        }

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

        /// <summary>The Bone Animated renderers of a figure.</summary>
        public static BoneAnimatedAuthoring[] Renderers(Transform figure) =>
            figure != null ? figure.GetComponentsInChildren<BoneAnimatedAuthoring>(true) : Array.Empty<BoneAnimatedAuthoring>();

        /// <summary>The clip table of a figure, which every renderer of it must share; null when it has none or they differ.</summary>
        public static BoneClipTable Table(Transform figure)
        {
            var renderers = Renderers(figure);
            if (renderers.Length == 0 || renderers[0].clips == null)
            {
                return null;
            }

            foreach (var renderer in renderers)
            {
                if (renderer.clips != renderers[0].clips)
                {
                    return null;
                }
            }

            return renderers[0].clips;
        }

        /// <summary>
        /// Each figure's clips from its own table by this squad's clip names; fails when a figure or its table is
        /// missing, or a clip is missing from it.
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
                var table = Table(figures[i].root);
                if (table == null)
                {
                    throw new InvalidOperationException(
                        $"Squad Figures on '{name}' needs every figure to have Bone Animated renderers that share one clip table.");
                }

                clips[i] = Clips(table, figures[i].clipPrefix ?? "");
            }

            return clips;
        }

        public SquadFigures Squad() => new() { RunSpeed = runSpeed, DieHold = dieHold };

        private FigureClips Clips(BoneClipTable table, string prefix)
        {
            var idle = table.Range(prefix + idleClip);
            var walk = table.Range(prefix + walkClip);
            var fire = table.Range(prefix + fireClip);
            return new FigureClips
            {
                Idle = idle,
                Walk = walk,
                Run = table.Range(prefix + runClip),
                Fire = fire,
                CrouchIdle = OrElse(table, prefix, crouchIdleClip, idle),
                CrouchWalk = OrElse(table, prefix, crouchWalkClip, walk),
                CrouchFire = OrElse(table, prefix, crouchFireClip, fire),
                ProneIdle = OrElse(table, prefix, proneIdleClip, idle),
                ProneCrawl = OrElse(table, prefix, proneCrawlClip, walk),
                ProneFire = OrElse(table, prefix, proneFireClip, fire),
                Dig = OrElse(table, "", digClip, idle),
                Die = table.Range(dieClip),
                DieBlastFront = OrElse(table, "", dieBlastFrontClip, default),
                DieBlastBack = OrElse(table, "", dieBlastBackClip, default),
                DieBlastLeft = OrElse(table, "", dieBlastLeftClip, default),
                DieBlastRight = OrElse(table, "", dieBlastRightClip, default),
            };
        }

        // Only an empty name takes the stand-in; a name the table lacks fails.
        private static VATClipRange OrElse(BoneClipTable table, string prefix, string clipName, VATClipRange standIn) =>
            string.IsNullOrEmpty(clipName) ? standIn : table.Range(prefix + clipName);

        public class Baker : Baker<SquadFiguresAuthoring>
        {
            public override void Bake(SquadFiguresAuthoring authoring)
            {
                var clips = authoring.Clips();
                var entities = new List<SquadFigure>();
                for (var i = 0; i < clips.Length; i++)
                {
                    var figure = authoring.figures[i].root;
                    DependsOn(Table(figure));
                    var entity = GetEntity(figure, TransformUsageFlags.Dynamic);
                    entities.Add(new SquadFigure { Figure = entity, Clips = clips[i], Height = figure.localPosition.y });
                }

                var writer = this.Writer();
                AddSquad(writer, authoring.Squad(), entities);
            }
        }
    }
}
