using System;
using HyperRTS.Editor.Maps.Json;
using Newtonsoft.Json.Linq;
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
            JToken root;
            try
            {
                root = MapJsonFormat.Parse(file.text);
                JsonUtility.FromJsonOverwrite(file.text, card);
            }
            catch (Exception e) when (e is ArgumentException or FormatException)
            {
                throw new InvalidOperationException($"HyperRTS: plant card '{path}' is not valid JSON: {e.Message}", e);
            }

            MapJsonFormat.RejectUnknown(root, MapJsonFormat.Parse(JsonUtility.ToJson(card)), "plant card", path);
            var sized = card.width > 0f && card.height > 0f;
            return sized ? card : throw new InvalidOperationException($"HyperRTS: plant card '{path}' needs a width and height above 0.");
        }
    }
}
