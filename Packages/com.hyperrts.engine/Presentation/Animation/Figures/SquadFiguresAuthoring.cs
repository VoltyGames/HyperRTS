using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    /// <summary>
    /// Draws a squad unit as bone-animated figures, one per Squad member. Each figure is a child whose Bone Animated
    /// renderers share one clip table, and its Figure Clip Set picks the clips it plays.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Look + "Animation/Squad Figures")]
    [Icon(HyperRTSIcons.SquadFiguresAuthoring)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class SquadFiguresAuthoring : AuthoringBehaviour
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

        [Tooltip("Horizontal speed from which figures run instead of walk.")]
        [Unit("m/s")]
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

        public class Baker : Baker<SquadFiguresAuthoring>
        {
            public override void Bake(SquadFiguresAuthoring authoring)
            {
                if (authoring.figures.Count == 0)
                {
                    throw new InvalidOperationException($"Squad Figures on '{authoring.name}' lists no figures.");
                }

                var entities = new List<SquadFigure>();
                for (var i = 0; i < authoring.figures.Count; i++)
                {
                    var figure = authoring.figures[i];
                    entities.Add(new SquadFigure
                    {
                        Figure = GetEntity(figure.root, TransformUsageFlags.Dynamic),
                        Clips = Clips(authoring, i, figure.root, figure.clips),
                        Height = figure.root.localPosition.y,
                        Baked = figure.root.localRotation,
                        Other = Variant(authoring, i),
                    });
                }

                var squad = authoring.Squad();
                if (authoring.deployedModel != null)
                {
                    squad.DeployedModel = GetEntity(authoring.deployedModel, TransformUsageFlags.Dynamic);
                }

                var writer = this.Writer();
                AddSquad(writer, squad, entities);
                writer.AddBuffer<FigureRenderer>();
            }

            private FigureVariant Variant(SquadFiguresAuthoring authoring, int index)
            {
                var baked = authoring.figures[index];
                if (baked.HasTwoVariants)
                {
                    throw new InvalidOperationException(
                        $"Squad Figures on '{authoring.name}': figure {index} has both an upgraded and an emplaced figure.");
                }

                if (baked.upgradedRoot != null)
                {
                    return Variant(authoring, index, baked.upgradedRoot, baked.upgradedClips, VariantReason.Upgraded);
                }

                if (baked.emplacedRoot != null)
                {
                    return Variant(authoring, index, baked.emplacedRoot, baked.emplacedClips, VariantReason.Emplaced);
                }

                return default;
            }

            private FigureVariant Variant(SquadFiguresAuthoring authoring, int index, Transform root, FigureClipSet set, VariantReason reason)
            {
                return new FigureVariant
                {
                    Reason = reason,
                    Model = new FigureModel
                    {
                        Figure = GetEntity(root, TransformUsageFlags.Dynamic),
                        Clips = Clips(authoring, index, root, set),
                        Height = root.localPosition.y,
                    },
                };
            }

            // The blob is shared with every figure whose set and table give the same ranges.
            private BlobAssetReference<FigureClips> Clips(SquadFiguresAuthoring authoring, int index, Transform model, FigureClipSet set)
            {
                var clips = authoring.BuildClips(index, model, set);
                DependsOn(Table(model));
                DependsOn(set);
                AddBlobAsset(ref clips, out _);
                return clips;
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
        /// The clip ranges <paramref name="set"/> picks from the clip table of <paramref name="model"/>, owned by the caller;
        /// fails when the set doesn't fit it.
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
