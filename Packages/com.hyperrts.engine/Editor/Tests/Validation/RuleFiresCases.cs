using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Rules;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Upgrades;
using HyperRTS.Simulation.Veterancy;
using Unity.Scenes;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>One minimal broken case per rule, keyed by rule type, with a fragment of what it must report.</summary>
    public partial class RuleFiresTests
    {
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<Type, (string Expected, Func<RuleFiresTests, object> Build)> Rows =
            GameplayRows().Concat(EntityRows()).Concat(UnitRows()).Concat(EffectRows()).Concat(SceneRows())
                .ToDictionary(row => row.Rule, row => (row.Expected, row.Build));

        private static (Type Rule, string Expected, Func<RuleFiresTests, object> Build) Row<TRule>(string expected,
            Func<RuleFiresTests, object> build) => (typeof(TRule), expected, build);

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> GameplayRows()
        {
            yield return Row<AbilityRule>("'Smoke'", f =>
            {
                var abilities = f.Add<AbilityAuthoring>(EntityTemplates.CombatUnit());
                abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", damage = 1f });
                abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", damage = 1f });
                return abilities;
            });
            yield return Row<AirdropReceiverRule>("no aircraft prefab", f =>
                f.Add<AirdropReceiverAuthoring>(EntityTemplates.Building()));
            yield return Row<StrategicAssetRule>("no aircraft prefab", f =>
                f.Add<StrategicAssetAuthoring>(EntityTemplates.Building()));
            yield return Row<AreaFieldRule>("Affects nobody", f =>
            {
                var field = f.Add<AreaFieldAuthoring>(EntityTemplates.Building());
                field.affects = FieldTargets.Units;
                return field;
            });
            yield return Row<ArmorRule>("no damage type", f =>
            {
                var armor = f.Add<ArmorAuthoring>(EntityTemplates.Unit());
                armor.modifiers.Add(new ArmorAuthoring.Entry { multiplier = 2f });
                return armor;
            });
            yield return Row<BuilderRule>("is a scene object", f =>
            {
                var builder = f.Track(EntityTemplates.Worker()).GetComponent<BuilderAuthoring>();
                builder.buildOptions.Add(f.Track(EntityTemplates.Building()).GetComponent<BuildingAuthoring>());
                return builder;
            });
            yield return Row<ContainerRule>("exceeds capacity", f =>
            {
                var container = f.Add<ContainerAuthoring>(EntityTemplates.Building());
                container.capacity = 2;
                container.maxPassengerSize = 4;
                return container;
            });
            yield return Row<DeliverySourceRule>("no carrier prefab",
                f => f.Add<DeliverySourceAuthoring>(EntityTemplates.Building()));
        }

        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> EntityRows()
        {
            yield return Row<GameEntityRule>("Death spawn 'Wreck'", f =>
            {
                var unit = f.Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
                unit.deathSpawn = f.Track(EntityTemplates.Unit("Wreck"));
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
            yield return Row<InterceptorRule>("no Deployable", f =>
            {
                var interceptor = f.Add<InterceptorAuthoring>(EntityTemplates.Unit());
                interceptor.needsDeploy = true;
                return interceptor;
            });
            yield return Row<LocomotionRule>("has no wheelbase", f =>
            {
                var locomotion = f.Add<LocomotionAuthoring>(EntityTemplates.Unit());
                locomotion.pivots = false;
                locomotion.turnRadius = 9f;
                return locomotion;
            });
            yield return Row<MachinePartRule>("To must be above From", f =>
            {
                var part = f.Add<MachinePartAuthoring>(new GameObject("Jack"));
                part.motion = MachineMotion.Extend;
                part.from = 0.5f;
                part.to = 0.5f;
                return part;
            });
            yield return Row<SceneryRemainsRule>("show twice", f =>
            {
                var scenery = f.Add<DestructibleAuthoring>(new GameObject("Fence"));
                scenery.remains = f.Track(EntityTemplates.Unit("Rubble"));
                return scenery;
            });
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
            yield return Row<WeaponRule>("is a scene object", f =>
            {
                var weapon = f.Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
                weapon.stats.projectilePrefab = f.Make("Shell");
                return weapon;
            });
        }

        // An empty required rig, or a scene object where a prefab asset belongs, is what each effect rule catches.
        private static IEnumerable<(Type Rule, string Expected, Func<RuleFiresTests, object> Build)> EffectRows()
        {
            yield return Row<BreakablePartsRule>("has no effect rig",
                f => f.Add<BreakablePartsAuthoring>(new GameObject("Parts")));
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
