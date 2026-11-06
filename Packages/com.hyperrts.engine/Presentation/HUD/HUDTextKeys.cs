using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common.Infantry;

namespace HyperRTS.Presentation.HUD
{
    public partial class HUDText
    {
        /// <summary>Every fixed key the HUD can ask for: its English table and the keys it builds from enum values.</summary>
        public static IEnumerable<string> Keys => English.Keys
            .Union(EnumKeys<Posture>("squad", value => value.ToString()))
            .Union(EnumKeys<SuppressionLevel>("squad", value => value.ToString()))
            .Union(EnumKeys<CoverKind>("squad", value => value + "Cover").Where(key => key != "squad.none-cover"))
            .Union(EnumKeys<MatchOutcome>("outcome", value => value.ToString()).Where(key => key != "outcome.none"));

        private static IEnumerable<string> EnumKeys<T>(string kind, Func<T, string> name) where T : struct, Enum =>
            Enum.GetValues(typeof(T)).Cast<T>().Select(value => Key(kind, name(value)));
    }
}
