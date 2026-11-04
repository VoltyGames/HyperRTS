using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.VAT;
using System;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class SquadFiguresAuthoring
    {
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

        /// <summary>The clips of figure <paramref name="index"/>'s other model <paramref name="root"/>; fails when its renderers don't share a clip table.</summary>
        public FigureClips VariantClips(int index, Transform root, string prefix)
        {
            var table = Table(root);
            if (table == null)
            {
                throw new InvalidOperationException(
                    $"Squad Figures on '{name}' needs the other figure {index} to have Bone Animated renderers that share one clip table.");
            }

            return Clips(table, prefix ?? "");
        }

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
    }
}
