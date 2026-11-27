using System;
using System.Linq;
using HyperRTS.Editor.Effects.Layer;
using UnityEditor;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>A layer's burst list as text, <c>delay:min-max</c> entries joined by commas, such as <c>0:1-1,0.7:2-3</c>.</summary>
internal readonly struct BurstText
{
    private readonly SerializedProperty _bursts;

    public BurstText(SerializedProperty bursts) => _bursts = bursts;

    public string Read()
    {
        var entries = new string[_bursts.arraySize];
        for (var i = 0; i < entries.Length; i++)
        {
            var burst = _bursts.GetArrayElementAtIndex(i);
            entries[i] = PropertyText.Text(new EffectBurst
            {
                delay = burst.FindPropertyRelative("delay").floatValue,
                count = new FloatRange(
                    burst.FindPropertyRelative("count.min").floatValue,
                    burst.FindPropertyRelative("count.max").floatValue),
                repeats = burst.FindPropertyRelative("repeats").intValue,
                interval = burst.FindPropertyRelative("interval").floatValue,
            });
        }

        return string.Join(",", entries);
    }

    /// <summary>Replaces the bursts; one added past the old end repeats nothing.</summary>
    public void Write(string text)
    {
        var entries = text.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var old = _bursts.arraySize;
        _bursts.arraySize = entries.Length;
        for (var i = 0; i < entries.Length; i++)
        {
            var burst = _bursts.GetArrayElementAtIndex(i);
            var parts = entries[i].Split(':');
            var counts = parts.Length == 2 ? parts[1].Split('-') : Array.Empty<string>();
            if (counts.Length is 0 or > 2)
            {
                throw new ArgumentException($"HyperRTS: '{entries[i]}' is not 'delay:min-max' for '{_bursts.propertyPath}'.");
            }

            var min = PropertyText.ParseFloat(counts[0], _bursts.propertyPath);
            burst.FindPropertyRelative("delay").floatValue = PropertyText.ParseFloat(parts[0], _bursts.propertyPath);
            burst.FindPropertyRelative("count.min").floatValue = min;
            burst.FindPropertyRelative("count.max").floatValue = counts.Length == 2 ? PropertyText.ParseFloat(counts[1], _bursts.propertyPath) : min;
            if (i >= old)
            {
                burst.FindPropertyRelative("repeats").intValue = 0;
                burst.FindPropertyRelative("interval").floatValue = 0f;
            }
        }
    }
}
