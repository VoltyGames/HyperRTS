using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>One frame's instances of one mesh, batched per faction and drawn in that faction's colour.</summary>
    public sealed class FactionBatches
    {
        private readonly Dictionary<byte, InstanceBatch> _batches = new();
        private readonly Mesh _mesh;

        public FactionBatches(Mesh mesh)
        {
            _mesh = mesh;
        }

        public void Add(byte faction, in Matrix4x4 matrix)
        {
            if (!_batches.TryGetValue(faction, out var batch))
            {
                batch = new InstanceBatch(_mesh);
                _batches.Add(faction, batch);
            }

            batch.Add(matrix);
        }

        public void Draw(Material material, MatchView view, int layer)
        {
            foreach (var (faction, batch) in _batches)
            {
                batch.Draw(material, view.GetColor(faction), layer);
            }
        }
    }
}
