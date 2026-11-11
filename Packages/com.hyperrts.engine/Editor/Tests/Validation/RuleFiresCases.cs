using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.Buildings;
using HyperRTS.Editor.Validation.Rules.Effects;
using HyperRTS.Editor.Validation.Rules.Fields;
using HyperRTS.Editor.Validation.Rules.Match;
using HyperRTS.Editor.Validation.Rules.Units;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Animation.Machines;
using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Presentation.Effects.Streams;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Cues.Effects;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Squads;
using HyperRTS.Simulation.Squads.Cover;
using HyperRTS.Simulation.Strikes;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Veterancy;
using Unity.Scenes;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>One minimal broken case per rule, keyed by rule type, with a fragment of what it must report.</summary>
    internal static class RuleFiresCases
    {
        [NoAutoStaticsCleanup]
        public static readonly Dictionary<Type, (string Expected, Func<RuleFiresTests, object> Build)> Rows =
            GameplayRows().Concat(SquadRows()).Concat(EntityRows()).Concat(UnitRows()).Concat(UnitStatRows()).Concat(EffectRows()).Concat(SceneRows())
                .ToDictionary(row => row.Rule, row => (row.Expected, row.Build));

        private static (Type Rule, string Expected, Func<RuleFiresTests, object> Build) Row<TRule>(string expected,
            Func<RuleFiresTests, object> build) => (typeof(TRule), expected, build);

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> GameplayRows()
        {
            yield return Row<AbilityRule>("'Smoke'", f =>
            {
                var abilities = f.Add<AbilityAuthoring>(EntityTemplates.CombatUnit());
                abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", hit = new DamageSettings { damage = 1f } });
                abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", hit = new DamageSettings { damage = 1f } });
                return abilities;
            });
            yield return Row<AirdropReceiverRule>("no aircraft prefab", f =>
                f.Add<AirdropReceiverAuthoring>(EntityTemplates.Building()));
            yield return Row<StrategicAssetRule>("no aircraft prefab", f =>
                f.Add<StrategicAssetAuthoring>(EntityTemplates.Building()));
            yield return Row<AuraRule>("Affects nobody", f =>
            {
                var field = f.Add<AuraAuthoring>(EntityTemplates.Building());
                field.affects = AuraTargets.Units;
                return field;
            });
            yield return Row<FieldAttributeRule>("Extra Weapons 1: Projectile 'Shell' is a scene object", f =>
            {
                var weapon = f.Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
                weapon.extraWeapons.Add(new WeaponAuthoring.WeaponSlot());
                weapon.extraWeapons[0].stats.projectilePrefab = f.Make("Shell");
                return weapon;
            });
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> SquadRows()
        {
            yield return Row<ContainerRule>("exceeds capacity", f =>
            {
                var container = f.Add<ContainerAuthoring>(EntityTemplates.Building());
                container.capacity = 2;
                container.maxPassengerSize = 4;
                return container;
            });
            yield return Row<SquadRule>("no members",
                f => f.Add<SquadAuthoring>(EntityTemplates.CombatUnit()));
            yield return Row<SquadFiguresRule>("both an upgraded and an emplaced figure", f =>
            {
                var squad = f.Add<SquadFiguresAuthoring>(EntityTemplates.CombatUnit());
                var root = new GameObject("Figure").transform;
                squad.figures.Add(new SquadFiguresAuthoring.Figure { root = root, upgradedRoot = root, emplacedRoot = root });
                return squad;
            });
            yield return Row<CoverSpotRule>("faces away from the prop", f =>
            {
                var cover = f.Add<CoverSpotAuthoring>(new GameObject("Wall"));
                cover.spots.Add(new CoverSpotAuthoring.Spot
                {
                    position = new Vector3(0f, 0f, -0.8f), facing = new Vector2(0f, -1f), kind = CoverKind.Heavy,
                });
                return cover;
            });
            yield return Row<DeliverySourceRule>("no carrier prefab",
                f => f.Add<DeliverySourceAuthoring>(EntityTemplates.Building()));
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> EntityRows()
        {
            yield return Row<GameEntityRule>("never met", f =>
            {
                var unit = f.Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
                unit.prerequisites.Add(f.Track(EntityTemplates.Unit("Scout")).GetComponent<UnitAuthoring>());
                return unit;
            });
            yield return Row<MatchRule>("no players", f =>
            {
                var match = f.Add<MatchAuthoring>(new GameObject("Match"));
                match.players.Clear();
                return match;
            });
            yield return Row<ProducerRule>("inside the footprint", f =>
            {
                var producer = f.Track(EntityTemplates.Producer()).GetComponent<ProducerAuthoring>();
                producer.spawnOffset = Vector3.zero;
                return producer;
            });
            yield return Row<RequiredAuthoringRule>("needs a Unit",
                f => f.Add<HarvesterAuthoring>(EntityTemplates.Building()));
            yield return Row<RequiresGhostRule>("Not a ghost",
                f => f.Add<ResourceNodeAuthoring>(new GameObject("Node")));
            yield return Row<ResourceNodeRule>("no resource type",
                f => f.Track(EntityTemplates.ResourceNode()).GetComponent<ResourceNodeAuthoring>());
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> UnitRows()
        {
            yield return Row<GarrisonPartRule>("no Container above it", f => f.Add<GarrisonPartAuthoring>(new GameObject("Gun")));
            yield return Row<LocomotionRule>("has no wheelbase", f =>
            {
                var locomotion = f.Add<LocomotionAuthoring>(EntityTemplates.Unit());
                locomotion.pivots = false;
                locomotion.turnRadius = 9f;
                return locomotion;
            });
            yield return Row<MachineRule>("Ramp Seconds must be above 0", f =>
            {
                var machine = f.Add<MachineAuthoring>(new GameObject("Door"));
                machine.rampSeconds = 0f;
                return machine;
            });
            yield return Row<MachinePartRule>("To must be above From", f =>
            {
                var part = f.Add<MachinePartAuthoring>(new GameObject("Jack"));
                part.motion = MachineMotion.Extend;
                part.from = 0.5f;
                part.to = 0.5f;
                return part;
            });
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> UnitStatRows()
        {
            yield return Row<SceneryRemainsRule>("show twice", f =>
            {
                var scenery = f.Add<DestructibleAuthoring>(new GameObject("Fence"));
                scenery.remains = f.Track(EntityTemplates.Unit("Rubble"));
                return scenery;
            });
            yield return Row<TurretTossRule>("hull and a turret", f => f.Add<TurretTossAuthoring>(new GameObject("Tank")));
            yield return Row<TeamColorRule>("owner's colour", f =>
            {
                var unit = f.Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
                var plain = f.Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
                unit.GetComponentInChildren<Renderer>().sharedMaterial = plain;
                return unit;
            });
            yield return Row<UnitRule>("no Flight component", f =>
            {
                var unit = f.Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
                unit.navLayer = NavLayer.Air;
                return unit;
            });
            yield return Row<UpgradeRule>("No stat bonuses",
                f => f.Track(EntityTemplates.Upgrade()).GetComponent<UpgradeAuthoring>());
            yield return Row<VeterancyRule>("No ranks", f => f.Add<VeterancyAuthoring>(EntityTemplates.CombatUnit()));
            yield return Row<WeaponRule>("negative arc height", f =>
            {
                var weapon = f.Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
                weapon.stats.arcHeight = -1f;
                return weapon;
            });
        }

        // An empty required rig, or a scene object where a prefab asset belongs, is what each effect rule catches.
        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> EffectRows()
        {
            yield return Row<SectionsRule>("has no effect rig",
                f => f.Add<SectionsAuthoring>(new GameObject("Sections")));
            yield return Row<DamageStageEffectRule>("is not a prefab asset", f =>
            {
                var damage = f.Add<DamageStagesAuthoring>(new GameObject("Damage"));
                damage.stages.Add(new DamageStagesAuthoring.Stage { effect = f.Make("Smoke") });
                return damage;
            });
            yield return Row<EffectTrailRule>("has no effect rig",
                f => f.Add<EffectTrailAuthoring>(new GameObject("Trail")));
            yield return Row<EntityEffectsRule>("has no muzzle socket", f =>
            {
                var effects = f.Add<EntityEffectsAuthoring>(new GameObject("Effects"));
                effects.fire = f.Make("Flash");
                return effects;
            });
            yield return Row<ImpactEffectsRule>("no surface effects asset",
                f => f.Add<ImpactEffectsAuthoring>(new GameObject("Impacts")));
            yield return Row<LoopEffectsRule>("has no socket", f =>
            {
                var effects = f.Add<LoopEffectsAuthoring>(new GameObject("Loops"));
                effects.loops = new[] { new LoopEffectsAuthoring.Entry() };
                return effects;
            });
            yield return Row<MotionDustRule>("has no socket", f => f.Add<MotionDustAuthoring>(new GameObject("Dust")));
            yield return Row<MotionRigRule>("has no part", f =>
            {
                var rig = f.Add<MotionRigAuthoring>(new GameObject("Rig"));
                rig.turrets.Add(new TurretSlot());
                return rig;
            });
            yield return Row<SpawnEffectRule>("has no effect rig",
                f => f.Add<SpawnEffectAuthoring>(new GameObject("Spawn")));
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> SceneRows()
        {
            yield return Row<DuplicateNameRule>("'Tank'", f => new PrefabSet(new[]
            {
                f.Track(EntityTemplates.Unit("Tank")), f.Track(EntityTemplates.Unit("Tank")),
            }));
            yield return Row<MatchCountRule>("More than one Match", f => f.InNewScene(false, () =>
            {
                f.Add<MatchAuthoring>(new GameObject("Match"));
                f.Add<MatchAuthoring>(new GameObject("Second Match"));
            }));
            yield return Row<MatchPlacementRule>("has no player slot", f => f.InNewScene(false, () =>
            {
                f.Add<MatchAuthoring>(new GameObject("Match"));
                f.Track(EntityTemplates.Unit()).GetComponent<GameEntityAuthoring>().owner = 3;
            }));
            // Inactive, so the SubScene doesn't start an edit-mode Entities world.
            yield return Row<NetcodeBootstrapRule>("No OverrideAutomaticNetcodeBootstrap",
                f => f.InNewScene(false, () =>
                {
                    var host = f.Make("SubScene");
                    host.SetActive(false);
                    host.AddComponent<SubScene>();
                }));
            yield return Row<SceneryIdRule>("same spot", f => f.InNewScene(true, () =>
            {
                f.Add<DestructibleAuthoring>(new GameObject("Rock"));
                f.Add<DestructibleAuthoring>(new GameObject("Second Rock"));
            }));
        }
    }
}
