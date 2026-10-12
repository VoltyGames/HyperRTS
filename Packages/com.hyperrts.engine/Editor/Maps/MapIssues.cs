using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps
{
    /// <summary>
    /// Errors and warnings found while building and measuring a map; errors fail the build. Each issue may name the
    /// spec feature it is about (<see cref="ErrorFeatures"/> and <see cref="WarningFeatures"/> run alongside).
    /// </summary>
    public sealed class MapIssues
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private readonly List<MapIssueFeature> _errorFeatures = new();
        private readonly List<MapIssueFeature> _warningFeatures = new();

        public IReadOnlyList<string> Errors => _errors;

        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>The feature of each error, index for index; <see cref="MapIssueFeature.None"/> for map-wide ones.</summary>
        public IReadOnlyList<MapIssueFeature> ErrorFeatures => _errorFeatures;

        /// <summary>The feature of each warning, index for index.</summary>
        public IReadOnlyList<MapIssueFeature> WarningFeatures => _warningFeatures;

        public bool HasErrors => _errors.Count > 0;

        public void Error(string message) => Error(message, MapIssueFeature.None);

        public void Error(string message, MapIssueFeature feature)
        {
            _errors.Add(message);
            _errorFeatures.Add(feature);
        }

        public void Warn(string message) => Warn(message, MapIssueFeature.None);

        public void Warn(string message, MapIssueFeature feature)
        {
            _warnings.Add(message);
            _warningFeatures.Add(feature);
        }

        public override string ToString() =>
            string.Join("\n", _errors.Select(e => "error: " + e).Concat(_warnings.Select(w => "warning: " + w)));
    }
}
