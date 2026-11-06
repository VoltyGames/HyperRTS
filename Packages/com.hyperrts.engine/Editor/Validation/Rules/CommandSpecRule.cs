using System.Collections.Generic;
using System.Linq;
using HyperRTS.Presentation.HUD.Commands;
using UnityEditor;
using UnityEngine.InputSystem;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Command specs name what they are for, and no hotkey is shared by two buttons that can share a page of a card
    /// or taken by an input binding.
    /// </summary>
    public sealed class CommandSpecRule : IAssetRule
    {
        // Rotating a building only works while placing, when the card's hotkeys are off, so a card may reuse its key.
        private const string RotateBuilding = "RotateBuilding";
        private const string KeyboardPrefix = "<Keyboard>/";

        public void Check(AssetSet assets, ValidationIssues issues)
        {
            var specs = assets.All<CommandSpec>();
            var bound = FindBoundKeys();
            foreach (var spec in specs)
            {
                CheckTarget(spec, issues);
                if (spec.Hotkey != Key.None && bound.TryGetValue(KeyName(spec.Hotkey), out var action))
                {
                    issues.Error(spec, $"Command spec '{spec.name}' uses {spec.HotkeyText}, which the input action {action} already takes.");
                }
            }

            for (var i = 0; i < specs.Count; i++)
            {
                for (var j = i + 1; j < specs.Count; j++)
                {
                    if (Clashes(specs[i], specs[j]))
                    {
                        issues.Error(specs[j], $"Command specs '{specs[i].name}' and '{specs[j].name}' both use {specs[i].HotkeyText} on page {specs[i].Page}; one press would act on both.");
                    }
                }
            }
        }

        private static void CheckTarget(CommandSpec spec, ValidationIssues issues)
        {
            var missing = spec.Target switch
            {
                CommandTarget.Prefab => spec.Prefab == null ? "a prefab" : null,
                CommandTarget.Ability => string.IsNullOrEmpty(spec.Ability) ? "an ability name" : null,
                _ => string.IsNullOrEmpty(spec.LabelKey) ? "a label key" : null,
            };
            if (missing != null)
            {
                issues.Error(spec, $"Command spec '{spec.name}' is for {spec.Target} and needs {missing}.");
            }
        }

        // Specs on one card, or one without a card and any card, can be on screen together.
        private static bool Clashes(CommandSpec spec, CommandSpec other) =>
            spec.Hotkey != Key.None && other.Hotkey == spec.Hotkey && other.Page == spec.Page
            && (spec.Card == null || other.Card == null || spec.Card == other.Card);

        private static Dictionary<string, string> FindBoundKeys()
        {
            var bound = new Dictionary<string, string>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("RTSInputActions t:InputActionAsset").Single()));
            foreach (var binding in asset.actionMaps.SelectMany(map => map.bindings))
            {
                var isKey = binding.path.StartsWith(KeyboardPrefix) && binding.action != RotateBuilding;
                if (isKey)
                {
                    bound[binding.path.Substring(KeyboardPrefix.Length).ToLowerInvariant()] = binding.action;
                }
            }

            return bound;
        }

        private static string KeyName(Key key) => key.ToString().Replace("Digit", "").ToLowerInvariant();
    }
}
