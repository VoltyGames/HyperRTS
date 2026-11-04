using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    public partial class SquadFiguresAuthoring
    {
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
                    var baked = authoring.figures[i];
                    entities.Add(new SquadFigure
                    {
                        Figure = entity, Clips = clips[i], Height = figure.localPosition.y,
                        Weapon = baked.weaponName,
                        Other = Variant(authoring, i),
                        Crew = new CrewPlace { Takes = baked.takesCrewPlace, Offset = baked.crewOffset, Posture = baked.crewPosture },
                    });
                }

                var squad = authoring.Squad();
                if (authoring.deployedModel != null)
                {
                    squad.DeployedModel = GetEntity(authoring.deployedModel, TransformUsageFlags.Dynamic);
                }

                AddSquad(this.Writer(), squad, entities);
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
                    return Variant(authoring, index, baked.upgradedRoot, baked.upgradedClipPrefix, baked.upgradedWeaponName, VariantReason.Upgraded);
                }

                if (baked.emplacedRoot != null)
                {
                    return Variant(authoring, index, baked.emplacedRoot, baked.emplacedClipPrefix, baked.emplacedWeaponName, VariantReason.Emplaced);
                }

                return default;
            }

            private FigureVariant Variant(SquadFiguresAuthoring authoring, int index, Transform root, string prefix, string weapon, VariantReason reason)
            {
                DependsOn(Table(root));
                return new FigureVariant
                {
                    Reason = reason,
                    Model = new FigureModel
                    {
                        Figure = GetEntity(root, TransformUsageFlags.Dynamic),
                        Clips = authoring.VariantClips(index, root, prefix),
                        Height = root.localPosition.y,
                        Weapon = weapon,
                    },
                };
            }
        }
    }
}
