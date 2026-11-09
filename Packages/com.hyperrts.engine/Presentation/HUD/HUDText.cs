using System.Collections.Generic;
using System.Text;
using HyperRTS.Presentation.HUD.Panels;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Every string the HUD shows, by stable key. This base is the engine's English; a game subclasses it to translate
    /// and sets <see cref="Current"/> before the HUD builds.
    /// </summary>
    public partial class HUDText
    {
        [NoAutoStaticsCleanup]
        private static readonly Dictionary<string, string> English = new()
        {
            ["hud.pop"] = "POP",
            ["hud.power"] = "PWR",
            ["hud.strategic-ready"] = "READY",
            ["hud.rotate-hint"] = "Drag or hold {0}: rotate, {0}: +45°, {1}: snap",
            ["hud.wall-hint"] = "{0}: start or turn a corner, {1}: finish, {2}: snap",
            ["command.return"] = "Return",
            ["command.unload"] = "Unload",
            ["command.deploy"] = "Deploy",
            ["command.pack-up"] = "Pack Up",
            ["command.sell"] = "Sell",
            ["command.more"] = "More",
            ["command.back"] = "Back",
            ["command.retreat"] = "Retreat",
            ["command.reinforce"] = "Reinforce",
            ["squad.stand"] = "Standing",
            ["squad.crouch"] = "Crouched",
            ["squad.prone"] = "Prone",
            ["squad.heavy-cover"] = "Heavy cover",
            ["squad.light-cover"] = "Light cover",
            ["squad.dead"] = "Down",
            ["squad.steady"] = "Steady",
            ["squad.suppressed"] = "Suppressed",
            ["squad.pinned"] = "Pinned",
            ["squad.retreating"] = "Retreating",
            ["squad.reinforcing"] = "Reinforcing",
            ["tooltip.retreat"] = "Run to the nearest friendly retreat point, firing nothing and shaking off suppression.",
            ["tooltip.reinforce"] = "Bring back fallen soldiers one at a time near a friendly reinforce point.",
            ["tooltip.reinforce-time"] = "Time per soldier",
            ["tooltip.no-retreat-point"] = "No retreat point",
            ["tooltip.no-reinforce-point"] = "No reinforce point in range",
            ["tooltip.full-strength"] = "Already at full strength",
            ["tooltip.cannot-afford"] = "Not enough resources",
            ["stance.aggressive"] = "Aggressive",
            ["stance.defensive"] = "Defensive",
            ["stance.hold-position"] = "Hold",
            ["stance.passive"] = "Passive",
            ["outcome.victory"] = "VICTORY",
            ["outcome.defeat"] = "DEFEAT",
            ["outcome.draw"] = "DRAW",
            ["outcome.ended"] = "MATCH OVER",
            ["tooltip.build-time"] = "Build time",
            ["tooltip.limit"] = "Limit",
            ["squad.digging"] = "Digging",
            ["squad.building"] = "Building",
            ["tooltip.population"] = "Population",
            ["tooltip.power"] = "Power",
            ["tooltip.requires"] = "Requires",
            ["tooltip.seconds"] = "s",
        };

        [AutoStaticsCleanup]
        public static HUDText Current { get; set; } = new();

        /// <summary>Text of a fixed HUD key (hud.*, command.*, stance.*, outcome.*, tooltip.*).</summary>
        public virtual string Get(string key) => English[key];

        /// <summary>
        /// Shown name of authored content: <paramref name="key"/> is entity.*, resource.*, ability.* or weapon.* and
        /// <paramref name="authored"/> is the display name it was authored with.
        /// </summary>
        public virtual string Name(string key, string authored) => authored;

        /// <summary>Optional text of a content key such as entity.war-factory.description; null when there is none.</summary>
        public virtual string Description(string key) => null;

        public static string Text(string key) => Current.Get(key);

        /// <summary>A unit, building or upgrade by its <c>EntityInfo.Name</c>: entity.command-center.</summary>
        public static string EntityName(string name) => Current.Name(Key("entity", name), name);

        public static string EntityName(EntityManager entityManager, Entity entity) =>
            EntityName(entityManager.GetComponentData<EntityInfo>(entity).Name.ToString());

        /// <summary>The tooltip line under a unit's or building's name, keyed entity.war-factory.description.</summary>
        public static string EntityDescription(EntityManager entityManager, Entity entity)
        {
            var name = entityManager.GetComponentData<EntityInfo>(entity).Name.ToString();
            return Current.Description(Key("entity", name) + ".description");
        }

        /// <summary>Whole seconds with the unit: "12 s".</summary>
        public static string Seconds(float seconds) => $"{Mathf.CeilToInt(seconds)} {Text("tooltip.seconds")}";

        /// <summary>A weapon's designation as a member card shows it: weapon.m4a1 for M4A1.</summary>
        public static string WeaponName(string designation) => designation.Length == 0 ? "" : Current.Name(Key("weapon", designation), designation);

        public static string AbilityName(string name) => Current.Name(Key("ability", name), name);

        /// <summary>Keyed by the asset name, which is also the type's network id: resource.supplies.</summary>
        public static string ResourceName(ResourceType type) => Current.Name(Key("resource", type.name), type.displayName);

        public static string OutcomeTitle(MatchOutcome outcome) => Text(Key("outcome", outcome.ToString()));

        /// <summary>
        /// <paramref name="kind"/> and the name in lowercase kebab case: ("entity", "War Factory") gives
        /// entity.war-factory, ("stance", "HoldPosition") gives stance.hold-position.
        /// </summary>
        public static string Key(string kind, string name)
        {
            var key = new StringBuilder(kind).Append('.');
            var separate = false;
            var previous = ' ';
            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    separate = true;
                    previous = c;
                    continue;
                }

                var wordStart = char.IsUpper(c) && char.IsLower(previous);
                if ((separate || wordStart) && key[^1] != '.')
                {
                    key.Append('-');
                }

                key.Append(char.ToLowerInvariant(c));
                separate = false;
                previous = c;
            }

            return key.ToString();
        }
    }
}
