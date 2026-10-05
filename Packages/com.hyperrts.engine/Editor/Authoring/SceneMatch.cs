using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>The open scenes' Match, cached until the hierarchy changes, and its player colours.</summary>
    [InitializeOnLoad]
    public static class SceneMatch
    {
        private static MatchAuthoring _match;
        private static bool _stale = true;

        static SceneMatch() => EditorApplication.hierarchyChanged += () => _stale = true;

        public static MatchAuthoring Current
        {
            get
            {
                if (_stale || _match == null)
                {
                    _match = Object.FindAnyObjectByType<MatchAuthoring>();
                    _stale = false;
                }

                return _match;
            }
        }

        /// <summary>Grey for neutral, cyan for an owner with no player slot.</summary>
        public static Color PlayerColor(int owner)
        {
            if (owner == 0)
            {
                return Color.grey;
            }

            var match = Current;
            return match != null && owner <= match.players.Count ? match.players[owner - 1].color : Color.cyan;
        }
    }
}
