using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>Errors and warnings found while building and measuring a map; errors fail the build.</summary>
    public sealed class MapIssues
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public IReadOnlyList<string> Errors => _errors;

        public IReadOnlyList<string> Warnings => _warnings;

        public bool HasErrors => _errors.Count > 0;

        public void Error(string message) => _errors.Add(message);

        public void Warn(string message) => _warnings.Add(message);

        public override string ToString() =>
            string.Join("\n", _errors.Select(e => "error: " + e).Concat(_warnings.Select(w => "warning: " + w)));
    }
}
