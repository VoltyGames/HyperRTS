using System;
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

        /// <summary>Enters Play mode; an empty <paramref name="value"/> only enters it.</summary>
        public void Enter(string value)
        {
            if (value.Length > 0)
            {
                SessionState.SetString(_key, value);
            }

            EditorApplication.EnterPlaymode();
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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= DropUnlessEnteringPlay;
            SessionState.EraseString(_key);
        }
    }
}
