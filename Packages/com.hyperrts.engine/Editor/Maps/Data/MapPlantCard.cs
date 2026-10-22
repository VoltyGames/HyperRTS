using System;
using HyperRTS.Editor.Maps.Json;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A card plant's measured size in metres, read from the <c>&lt;Key&gt;.card.json</c> its card render writes.</summary>
    [Serializable]
    public sealed class MapPlantCard
    {
        public float width;
        public float height;

        public static MapPlantCard Read(TextAsset file)
        {
            var path = AssetDatabase.GetAssetPath(file);
            var card = new MapPlantCard();
            MapJsonFormat.ReadInto(file.text, card, "plant card", $"plant card '{path}'");
            var sized = card.width > 0f && card.height > 0f;
            return sized ? card : throw new InvalidOperationException($"HyperRTS: plant card '{path}' needs a width and height above 0.");
        }
    }
}
