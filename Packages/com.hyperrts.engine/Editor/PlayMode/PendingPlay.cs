using System;
using Unity.PlayMode.Editor;
using UnityEditor;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>
    /// Enters Play mode and hands a value to its owner once Play has started. The value is dropped when entering Play
    /// is refused (compile errors), so a later Play doesn't start it.
    /// </summary>
    internal sealed class PendingPlay
    {
        // SessionState survives the domain reload that entering Play mode may bring; a static would not.
        private readonly string _key;
        private readonly Action<string> _begin;

        /// <summary>Call it from the owner's [InitializeOnLoad] constructor, so it listens again after every reload.</summary>
        public PendingPlay(string key, Action<string> begin)
        {
            _key = key;
            _begin = begin;
            EditorApplication.playModeStateChanged += Begin;
            DropUnlessEnteringPlay();
        }

        /// <summary>
        /// Enters Play mode; an empty <paramref name="value"/> only enters it. With <paramref name="throughScenario"/> the
        /// active Multiplayer Play Mode scenario starts instead, which also launches its other Editor instances.
        /// </summary>
        public void Enter(string value, bool throughScenario = false)
        {
            if (value.Length > 0)
            {
                SessionState.SetString(_key, value);
            }

            if (throughScenario)
            {
                PlayModeScenarioManager.Start();
            }
            else
            {
                EditorApplication.EnterPlaymode();
            }

            EditorApplication.update += DropUnlessEnteringPlay;
        }

        private void Begin(PlayModeStateChange change)
        {
            var value = SessionState.GetString(_key, "");
            if (change != PlayModeStateChange.EnteredPlayMode || value.Length == 0)
            {
                return;
            }

            SessionState.EraseString(_key);
            _begin(value);
        }

        private void DropUnlessEnteringPlay()
        {
            // A scenario spends a while starting its instances before Play begins.
            if (EditorApplication.isPlayingOrWillChangePlaymode || PlayModeScenarioManager.State != PlayModeScenarioState.Idle)
            {
                return;
            }

            EditorApplication.update -= DropUnlessEnteringPlay;
            SessionState.EraseString(_key);
        }
    }
}
