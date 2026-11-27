using System;
using System.Collections.Frozen;
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
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Air;
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

namespace HyperRTS.Editor.Tests.Validation;

/// <summary>One minimal broken case per rule, keyed by rule type, with a fragment of what it must report.</summary>
internal static class RuleFiresCases
{
    [NoAutoStaticsCleanup]
    public static readonly FrozenDictionary<Type, RuleCase> Rows =
        new[] { Gameplay(), Squads(), Entities(), Units(), UnitStats(), Effects(), Scenes() }
            .SelectMany(cases => cases).ToFrozenDictionary(brokenCase => brokenCase.Rule);

    private static RuleCase Case<TRule>(string expected, Func<RuleFiresTests, object> build) =>
        new(typeof(TRule), expected, build);

    extension<T>(T target)
    {
        private T With(Action<T> setup)
        {
            setup(target);
            return target;
        }
    }

    // A model with a hull mesh but no Wheel_L parts, so the grime fill has a model to read and fails on it.
    private static GameObject TruckWithoutWheels()
    {
        var truck = new GameObject("Truck");
        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "Hull";
        hull.transform.SetParent(truck.transform, false);
        return truck;
    }

    private static IEnumerable<RuleCase> Gameplay()
    {
        yield return Case<AbilityRule>("'Smoke'", f => f.Add<AbilityAuthoring>(TemplateObjects.CombatUnit()).With(a =>
        {
            a.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", hit = new DamageSettings { damage = 1f } });
            a.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", hit = new DamageSettings { damage = 1f } });
        }));
        yield return Case<AirdropReceiverRule>("no aircraft prefab",
            f => f.Add<AirdropReceiverAuthoring>(TemplateObjects.Building()));
        yield return Case<BomberStrikeRule>("no aircraft prefab", f => f.Add<BomberStrikeAuthoring>(TemplateObjects.Building()));
        yield return Case<MissileStrikeRule>("no missile prefab", f => f.Add<MissileStrikeAuthoring>(TemplateObjects.Building()));
        yield return Case<AircraftRemainsRule>("don't fall", f => f.Template<UnitAuthoring>(TemplateObjects.Aircraft())
            .With(aircraft => aircraft.remains = f.Make("Wreck")).GetComponent<FlightAuthoring>());
        yield return Case<AuraRule>("Affects nobody", f => f.Add<AuraAuthoring>(TemplateObjects.Building())
            .With(aura => aura.affects = AuraTargets.Units));
        yield return Case<ModelFillRule>("no road wheels", f => f.Add<GrimeAuthoring>(TruckWithoutWheels()));
        yield return Case<FieldAttributeRule>("Extra Weapons 1: Projectile 'Shell' is a scene object", f =>
            f.Template<WeaponAuthoring>(TemplateObjects.CombatUnit()).With(weapon =>
            {
                weapon.extraWeapons.Add(new WeaponAuthoring.WeaponSlot());
                weapon.extraWeapons[0].stats.projectilePrefab = f.Make("Shell");
            }));
    }

    private static IEnumerable<RuleCase> Squads()
    {
        yield return Case<ContainerRule>("exceeds capacity", f => f.Add<ContainerAuthoring>(TemplateObjects.Building())
            .With(container =>
            {
                container.capacity = 2;
                container.maxPassengerSize = 4;
            }));
        yield return Case<SquadRule>("no members", f => f.Add<SquadAuthoring>(TemplateObjects.CombatUnit()));
        yield return Case<SquadFiguresRule>("both an upgraded and an emplaced figure", f =>
        {
            var root = f.Make("Figure").transform;
            return f.Add<SquadFiguresAuthoring>(TemplateObjects.CombatUnit()).With(squad =>
                squad.figures.Add(new SquadFiguresAuthoring.Figure { root = root, upgradedRoot = root, emplacedRoot = root }));
        });
        yield return Case<CoverSpotRule>("faces away from the prop", f => f.Add<CoverSpotAuthoring>(new GameObject("Wall"))
            .With(cover => cover.spots.Add(new CoverSpotAuthoring.Spot
            {
                position = new Vector3(0f, 0f, -0.8f), facing = new Vector2(0f, -1f), kind = CoverKind.Heavy,
            })));
        yield return Case<DeliverySourceRule>("no carrier prefab",
            f => f.Add<DeliverySourceAuthoring>(TemplateObjects.Building()));
    }

    private static IEnumerable<RuleCase> Entities()
    {
        yield return Case<GameEntityRule>("never met", f => f.Template<UnitAuthoring>(TemplateObjects.Unit())
            .With(unit => unit.prerequisites.Add(f.Template<UnitAuthoring>(TemplateObjects.Unit("Scout")))));
        yield return Case<MatchRule>("no players", f => f.Add<MatchAuthoring>(new GameObject("Match"))
            .With(match => match.players.Clear()));
        yield return Case<ProducerRule>("inside the footprint", f => f.Template<ProducerAuthoring>(TemplateObjects.Producer())
            .With(producer => producer.spawnOffset = Vector3.zero));
        yield return Case<RequiredAuthoringRule>("needs a Unit", f => f.Add<HarvesterAuthoring>(TemplateObjects.Building()));
        yield return Case<RequiresGhostRule>("Not a ghost", f => f.Add<ResourceNodeAuthoring>(new GameObject("Node")));
        yield return Case<ResourceNodeRule>("no resource type",
            f => f.Template<ResourceNodeAuthoring>(TemplateObjects.ResourceNode()));
    }

    private static IEnumerable<RuleCase> Units()
    {
        yield return Case<GarrisonPartRule>("no Container above it", f => f.Add<GarrisonPartAuthoring>(new GameObject("Gun")));
        yield return Case<LocomotionRule>("has no wheelbase", f => f.Add<LocomotionAuthoring>(TemplateObjects.Unit())
            .With(locomotion =>
            {
                locomotion.pivots = false;
                locomotion.turnRadius = 9f;
            }));
        yield return Case<MachineRule>("Ramp Seconds must be above 0", f => f.Add<MachineAuthoring>(new GameObject("Door"))
            .With(machine => machine.rampSeconds = 0f));
        yield return Case<MachinePartRule>("To must be above From", f => f.Add<MachinePartAuthoring>(new GameObject("Jack"))
            .With(part =>
            {
                part.motion = MachineMotion.Extend;
                part.from = 0.5f;
                part.to = 0.5f;
            }));
    }

    private static IEnumerable<RuleCase> UnitStats()
    {
        yield return Case<SceneryRemainsRule>("show twice", f => f.Add<DestructibleAuthoring>(new GameObject("Fence"))
            .With(scenery => scenery.remains = f.Track(TemplateObjects.Unit("Rubble"))));
        yield return Case<TurretTossRule>("hull and a turret", f => f.Add<TurretTossAuthoring>(new GameObject("Tank")));
        yield return Case<TeamColorRule>("owner's colour", f =>
        {
            var plain = f.Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            return f.Template<UnitAuthoring>(TemplateObjects.Unit()).With(unit =>
            {
                foreach (var renderer in unit.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = plain;
                }
            });
        });
        yield return Case<UnitRule>("no Flight component", f => f.Template<UnitAuthoring>(TemplateObjects.Unit())
            .With(unit => unit.navLayer = NavLayer.Air));
        yield return Case<UpgradeRule>("No stat bonuses", f => f.Template<UpgradeAuthoring>(TemplateObjects.Upgrade()));
        yield return Case<VeterancyRule>("No ranks", f => f.Add<VeterancyAuthoring>(TemplateObjects.CombatUnit()));
        yield return Case<WeaponRule>("negative arc height", f => f.Template<WeaponAuthoring>(TemplateObjects.CombatUnit())
            .With(weapon => weapon.stats.arcHeight = -1f));
    }

    // An empty required rig, or a scene object where a prefab asset belongs, is what the effect rules catch.
    private static IEnumerable<RuleCase> Effects()
    {
        yield return Case<SectionsRule>("has no effect rig", f => f.Add<SectionsAuthoring>(new GameObject("Sections")));
        yield return Case<EffectReferencesRule>("has no socket", f => f.Add<LoopEffectsAuthoring>(new GameObject("Loops"))
            .With(effects => effects.loops = [new LoopEffectsAuthoring.Entry()]));
        yield return Case<MotionRigRule>("has no part", f => f.Add<MotionRigAuthoring>(new GameObject("Rig"))
            .With(rig => rig.turrets.Add(new TurretSlot())));
    }

    private static IEnumerable<RuleCase> Scenes()
    {
        yield return Case<DuplicateNameRule>("'Tank'", f =>
            new PrefabSet(new[] { f.Track(TemplateObjects.Unit("Tank")), f.Track(TemplateObjects.Unit("Tank")) }));
        yield return Case<MatchCountRule>("More than one Match", f => f.InNewScene(false, () =>
        {
            f.Add<MatchAuthoring>(new GameObject("Match"));
            f.Add<MatchAuthoring>(new GameObject("Second Match"));
        }));
        yield return Case<MatchPlacementRule>("has no player slot", f => f.InNewScene(false, () =>
        {
            f.Add<MatchAuthoring>(new GameObject("Match"));
            f.Template<GameEntityAuthoring>(TemplateObjects.Unit()).owner = 3;
        }));
        // Inactive, so the SubScene doesn't start an edit-mode Entities world.
        yield return Case<NetcodeBootstrapRule>("No OverrideAutomaticNetcodeBootstrap", f => f.InNewScene(false, () =>
        {
            var host = f.Make("SubScene");
            host.SetActive(false);
            host.AddComponent<SubScene>();
        }));
        yield return Case<SceneryIdRule>("same spot", f => f.InNewScene(true, () =>
        {
            f.Add<DestructibleAuthoring>(new GameObject("Rock"));
            f.Add<DestructibleAuthoring>(new GameObject("Second Rock"));
        }));
    }
}
