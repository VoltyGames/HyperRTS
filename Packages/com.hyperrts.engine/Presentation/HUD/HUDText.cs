using System.Collections.Generic;
using System.Text;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Every string the HUD shows, by stable key. This base is the engine's English; a game subclasses it to translate
    /// and sets <see cref="Current"/> before the HUD builds.
    /// </summary>
    public class HUDText
    {
        private static readonly Dictionary<string, string> English = new()
        {
            ["hud.pop"] = "POP",
            ["hud.power"] = "PWR",
            ["command.return"] = "Return",
            ["command.unload"] = "Unload",
            ["command.sell"] = "Sell",
            ["stance.aggressive"] = "Aggressive",
            ["stance.defensive"] = "Defensive",
            ["stance.hold-position"] = "Hold",
            ["stance.passive"] = "Passive",
            ["outcome.victory"] = "VICTORY",
            ["outcome.defeat"] = "DEFEAT",
            ["outcome.draw"] = "DRAW",
        };

        public static HUDText Current { get; set; } = new();

        /// <summary>Text of a fixed HUD key (hud.*, command.*, stance.*, outcome.*).</summary>
        public virtual string Get(string key) => English[key];

        /// <summary>
        /// Shown name of authored content: <paramref name="key"/> is entity.*, resource.* or ability.* and
        /// <paramref name="authored"/> is the display name it was authored with.
        /// </summary>
        public virtual string Name(string key, string authored) => authored;

        public static string Text(string key) => Current.Get(key);

        /// <summary>A unit, building or upgrade by its <c>EntityInfo.Name</c>: entity.command-center.</summary>
        public static string EntityName(string name) => Current.Name(Key("entity", name), name);

        public static string AbilityName(string name) => Current.Name(Key("ability", name), name);

        /// <summary>Keyed by the asset name, which is also the type's network id: resource.supplies.</summary>
        public static string ResourceName(ResourceType type) => Current.Name(Key("resource", type.name), type.displayName);

        public static string StanceName(Stance stance) => Text(Key("stance", stance.ToString()));

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = new HUDText();
    }
}
