using System;
using System.IO;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// A VAT bake info file, <c>VAT/&lt;Model&gt;.json</c> beside its model: the textures it names, their layout,
    /// the clips and the all-frame bounds. Keys it does not list are ignored.
    /// </summary>
    [Serializable]
    public class VATBakeInfo
    {
        public const string Folder = "VAT";

        public string name;
        public string positions;
        public string normals;
        public int width;
        public int height;
        public int rowsPerFrame;
        public int vertexCount;
        public VATClipTable.Clip[] clips;
        public float[] boundsMin;
        public float[] boundsMax;

        /// <summary>The bake info file a model at <paramref name="modelPath"/> would have.</summary>
        public static string PathFor(string modelPath) =>
            Path.Combine(Path.GetDirectoryName(modelPath) ?? "", Folder, Path.GetFileNameWithoutExtension(modelPath) + ".json")
                .Replace('\\', '/');

        /// <summary>Parses and checks a bake info file; throws naming the first bad key.</summary>
        public static VATBakeInfo Parse(string json, string source)
        {
            var bakeInfo = JsonUtility.FromJson<VATBakeInfo>(json);
            bakeInfo.Check(source);
            return bakeInfo;
        }

        public static Vector3 Corner(float[] values) => new(values[0], values[1], values[2]);

        private void Check(string source)
        {
            if (width < 1 || rowsPerFrame < 1)
            {
                throw new InvalidDataException($"VAT bake info '{source}' needs 'width' and 'rowsPerFrame' of at least 1.");
            }

            if (clips == null || clips.Length == 0)
            {
                throw new InvalidDataException($"VAT bake info '{source}' has no 'clips'.");
            }

            foreach (var clip in clips)
            {
                CheckClip(clip, source);
            }

            CheckCorner(boundsMin, "boundsMin", source);
            CheckCorner(boundsMax, "boundsMax", source);
        }

        private static void CheckClip(VATClipTable.Clip clip, string source)
        {
            if (string.IsNullOrEmpty(clip.name))
            {
                throw new InvalidDataException($"VAT bake info '{source}' has a clip without a 'name'.");
            }

            var valid = clip.startRow >= 0 && clip.frameCount >= 1 && clip.fps > 0f;
            if (!valid)
            {
                throw new InvalidDataException(
                    $"VAT bake info '{source}' clip '{clip.name}' needs 'startRow' >= 0, 'frameCount' >= 1 and 'fps' > 0.");
            }
        }

        private static void CheckCorner(float[] values, string key, string source)
        {
            if (values == null || values.Length != 3)
            {
                throw new InvalidDataException($"VAT bake info '{source}' needs '{key}' as [x, y, z].");
            }
        }
    }
}
