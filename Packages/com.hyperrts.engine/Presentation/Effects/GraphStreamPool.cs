using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// A rig's copies of its stream graphs, one set per playing stream. A stopped set waits until its particles have
    /// died before it serves another stream, so stopping never cuts a trail or a loop short.
    /// </summary>
    public sealed class GraphStreamPool
    {
        private readonly VisualEffect[] _templates;
        private readonly Stack<GraphStream> _idle = new();
        private readonly List<GraphStream> _stopping = new();

        public GraphStreamPool(VisualEffect[] effects) => _templates = Array.FindAll(effects, GraphStream.Streams);

        /// <summary>Whether the rig has any graph that streams by rate.</summary>
        public bool HasStreams => _templates.Length > 0;

        /// <summary>A started set for a new stream: an idle one, or fresh copies beside the rig's graphs.</summary>
        public GraphStream Start()
        {
            var stream = _idle.Count > 0 ? _idle.Pop() : new GraphStream(Array.ConvertAll(_templates, Copy));
            stream.Start();
            return stream;
        }

        /// <summary>Stops the stream's graphs and keeps the set until its particles are gone.</summary>
        public void Stop(GraphStream stream)
        {
            stream.Stop();
            _stopping.Add(stream);
        }

        /// <summary>Makes stopped sets whose particles have all died idle again.</summary>
        public void Tick()
        {
            for (var i = _stopping.Count - 1; i >= 0; i--)
            {
                if (_stopping[i].IsDone)
                {
                    _idle.Push(_stopping[i]);
                    _stopping.RemoveAt(i);
                }
            }
        }

        private static VisualEffect Copy(VisualEffect template)
        {
            var copy = Object.Instantiate(template.gameObject, template.transform.parent);
            copy.hideFlags = template.gameObject.hideFlags;
            return copy.GetComponent<VisualEffect>();
        }
    }
}
