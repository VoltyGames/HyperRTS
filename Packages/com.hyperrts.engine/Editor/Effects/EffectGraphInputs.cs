using System.Linq;
using HyperRTS.Presentation.Effects;
using UnityEngine;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// What every system of an effect graph reads: the events that start it, the Density property, the burst pose the
    /// Burst event carries and, on a graph with rate layers, the stream properties the runtime sets each frame.
    /// </summary>
    public sealed class EffectGraphInputs
    {
        private static readonly string[] PoseAttributes = { "position", "direction", "axisZ", "scale" };

        public EffectGraphInputs(GraphModel graph, bool streams)
        {
            Burst = Event(graph, EffectGraph.BurstEvent, -400f);
            Density = graph.Parameter(EffectGraph.DensityProperty, 1f);
            BurstPose = PoseAttributes.Select((attribute, i) => graph.SourceAttribute(attribute, new Vector2(-600f, 200f + 120f * i))).ToArray();
            if (!streams)
            {
                return;
            }

            Play = Event(graph, GraphStream.PlayEvent, -700f);
            Stop = Event(graph, GraphStream.StopEvent, -1000f);
            StreamPose = new[]
            {
                graph.Parameter(GraphStream.FromProperty, Vector3.zero), graph.Parameter(GraphStream.ToProperty, Vector3.zero),
                graph.Parameter(GraphStream.UpProperty, Vector3.up), graph.Parameter(GraphStream.ForwardProperty, Vector3.forward),
                graph.Parameter(GraphStream.ScaleProperty, 1f),
            };
            Speed = graph.Parameter(GraphStream.SpeedProperty, 0f);
        }

        public object Burst { get; }

        public object Play { get; }

        public object Stop { get; }

        public object Density { get; }

        /// <summary>The Burst event's position, direction, axisZ and scale, for Place At Burst.</summary>
        public object[] BurstPose { get; }

        /// <summary>From, To, Up, Forward and Scale, for Place Along Path.</summary>
        public object[] StreamPose { get; }

        /// <summary>Metres a second the stream moved this frame.</summary>
        public object Speed { get; }

        private static object Event(GraphModel graph, string name, float y)
        {
            var context = graph.Context("VFXBasicEvent", new Vector2(0f, y));
            graph.Set(context, "eventName", name);
            return context;
        }
    }
}
