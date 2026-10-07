using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Hands a <see cref="MatchSetup"/> from menu code, which runs before the match worlds and scene exist, to every
    /// world that loads the match. It stays set until cleared, so a host's server and client worlds both read it.
    /// </summary>
    public static class MatchSetupRequest
    {
        private static MatchSetup? _pending;

        public static void Set(in MatchSetup setup) => _pending = setup;

        public static void Clear() => _pending = null;

        public static bool TryGet(out MatchSetup setup)
        {
            setup = _pending.GetValueOrDefault();
            return _pending.HasValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _pending = null;
    }
}
