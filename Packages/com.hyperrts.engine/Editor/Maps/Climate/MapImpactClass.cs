using System;
using HyperRTS.Simulation.Cues;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>Reads a surface's or road's <c>impactClass</c>: a <see cref="SurfaceMaterial"/> name other than None.</summary>
    public static class MapImpactClass
    {
        public static bool IsValid(string value) => TryParse(value, out _);

        public static string Problem(string owner, string value) =>
            $"{owner}: impactClass '{value}' must name a SurfaceMaterial other than None ({string.Join(", ", Enum.GetNames(typeof(SurfaceMaterial)))}).";

        public static SurfaceMaterial Parse(string owner, string value) =>
            TryParse(value, out var material) ? material : throw new InvalidOperationException(Problem(owner, value));

        // Case-sensitive and names only, so "1" or "soil" in a JSON file is an error rather than a guess.
        private static bool TryParse(string value, out SurfaceMaterial material)
        {
            material = SurfaceMaterial.None;
            if (string.IsNullOrEmpty(value) || !Enum.IsDefined(typeof(SurfaceMaterial), value))
            {
                return false;
            }

            material = Enum.Parse<SurfaceMaterial>(value);
            return material != SurfaceMaterial.None;
        }
    }
}
