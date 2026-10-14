using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>Checks a climate's weathers: at least one, each with a unique key and an atmosphere prefab.</summary>
    public static class MapWeatherCheck
    {
        public static void Check(MapClimate climate, MapIssues issues)
        {
            var name = $"Climate '{climate.name}'";
            if (climate.weathers.Count == 0)
            {
                issues.Error($"{name} lists no weathers.");
            }

            var keys = new HashSet<string>();
            foreach (var weather in climate.weathers)
            {
                if (string.IsNullOrEmpty(weather.key))
                {
                    issues.Error($"{name}: a weather has no key.");
                    continue;
                }

                if (!keys.Add(weather.key))
                {
                    issues.Error($"{name}: weather '{weather.key}' is listed twice.");
                }

                if (weather.atmosphere == null)
                {
                    issues.Error($"{name}: weather '{weather.key}' has no atmosphere prefab.");
                }
            }
        }
    }
}
