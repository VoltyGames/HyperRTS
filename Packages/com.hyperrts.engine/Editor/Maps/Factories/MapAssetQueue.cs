using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps.Factories
{
    /// <summary>
    /// Library files waiting for their assets to be regenerated: an import batch adds them, one flush takes them all.
    /// <see cref="Add"/> reports when a flush must be scheduled, so a batch of imports schedules exactly one.
    /// </summary>
    public sealed class MapAssetQueue
    {
        private readonly List<string> _files = new();

        public int Count => _files.Count;

        /// <summary>Queues <paramref name="file"/> once; true when the queue was empty, so the caller schedules a flush.</summary>
        public bool Add(string file)
        {
            var first = _files.Count == 0;
            if (!_files.Contains(file, StringComparer.Ordinal))
            {
                _files.Add(file);
            }

            return first;
        }

        /// <summary>Every queued file in the order first added; empties the queue.</summary>
        public IReadOnlyList<string> Take()
        {
            var files = _files.ToList();
            _files.Clear();
            return files;
        }
    }
}
