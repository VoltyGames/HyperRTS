using System.Collections.Generic;
using HyperRTS.Simulation.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor
{
    /// <summary>The entities SubScene while <see cref="RTSSceneBuilder"/> builds it, for a spec's furnish step.</summary>
    public sealed class RTSSubScene
    {
        public RTSSubScene(Scene scene, MatchAuthoring match, IReadOnlyList<Vector3> bases)
        {
            Scene = scene;
            Match = match;
            Bases = bases;
        }

        public Scene Scene { get; }

        public MatchAuthoring Match { get; }

        /// <summary>Each player's base position, player 1 first, whether or not a starting base was placed.</summary>
        public IReadOnlyList<Vector3> Bases { get; }
    }
}
