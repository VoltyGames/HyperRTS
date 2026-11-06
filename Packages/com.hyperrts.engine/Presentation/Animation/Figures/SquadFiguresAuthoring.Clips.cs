using HyperRTS.Presentation.Animation.Bones;
using Unity.Entities;
using System;
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
        /// The clip ranges <paramref name="set"/> picks from the clip table of <paramref name="model"/>, which the caller
        /// owns; fails when there is no set, the model's renderers don't share a table, or the set doesn't fit it.
        /// </summary>
        public BlobAssetReference<FigureClips> BuildClips(int index, Transform model, FigureClipSet set)
        {
            var table = Table(model);
            if (table == null)
            {
                throw new InvalidOperationException(
                    $"Squad Figures on '{name}' needs figure {index} to have Bone Animated renderers that share one clip table.");
            }

            if (set == null)
            {
                throw new InvalidOperationException($"Squad Figures on '{name}' gives figure {index} no Figure Clip Set.");
            }

            return set.Build(table);
        }

        /// <summary>Fails when a figure or its other model lacks a clip table, a set, or a clip its set names.</summary>
        public void CheckClips()
        {
            if (figures.Count == 0)
            {
                throw new InvalidOperationException($"Squad Figures on '{name}' lists no figures.");
            }

            for (var i = 0; i < figures.Count; i++)
            {
                var figure = figures[i];
                BuildClips(i, figure.root, figure.clips).Dispose();
                if (figure.upgradedRoot != null)
                {
                    BuildClips(i, figure.upgradedRoot, figure.upgradedClips).Dispose();
                }

                if (figure.emplacedRoot != null)
                {
                    BuildClips(i, figure.emplacedRoot, figure.emplacedClips).Dispose();
                }
            }
        }
    }
}
