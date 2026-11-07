using System;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draws each <see cref="UnitAttribute"/> field's unit as a dim suffix inside its input.</summary>
    public static class UnitSuffixes
    {
        /// <summary>Adds a suffix to every <c>[Unit]</c> field of <paramref name="type"/> drawn in <paramref name="root"/>.</summary>
        public static void Bind(VisualElement root, Type type)
        {
            foreach (var (path, unit) in ConditionalFields.Marked<UnitAttribute>(type))
            {
                ConditionalFields.WhenDrawn(root, path, field => Append(field, unit.Unit));
            }
        }

        // The field builds its input when the inspector binds, so it can be absent on the first try.
        private static bool Append(PropertyField field, string unit)
        {
            var input = field.Q(className: "unity-base-field__input");
            if (input == null)
            {
                return false;
            }

            field.AddToClassList("hrts-unit-field");
            var suffix = new Label(unit) { pickingMode = PickingMode.Ignore };
            suffix.AddToClassList("hrts-unit");
            input.Add(suffix);
            return true;
        }
    }
}
