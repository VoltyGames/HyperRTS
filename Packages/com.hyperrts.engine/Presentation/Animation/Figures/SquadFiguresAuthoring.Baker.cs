using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Figures
{
    public partial class SquadFiguresAuthoring
    {
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
    }
}
