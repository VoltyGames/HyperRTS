using HyperRTS.Simulation.Match;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>The open scenes' Match, cached until the hierarchy changes, and its player colours.</summary>
    public static partial class SceneMatch
    {
        [AutoStaticsCleanup]
        private static MatchAuthoring _match;
        [AutoStaticsCleanup]
        private static bool _stale = true;

        [InitializeOnLoadMethod]
        private static void WatchHierarchy() => EditorApplication.hierarchyChanged += MarkStale;

        public static MatchAuthoring Current
        {
            get
            {
                if (_stale)
                {
                    _match = Object.FindAnyObjectByType<MatchAuthoring>();
                    _stale = false;
                }

                return _match;
            }
        }

        private static void MarkStale() => _stale = true;

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
