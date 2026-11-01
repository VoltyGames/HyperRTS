using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>Times the stages of a map build in the order they ran, for the report.</summary>
    public sealed class MapStageTimer
    {
        private readonly List<MapStageTime> _stages = new();

        public IReadOnlyList<MapStageTime> Stages => _stages;

        public void Time(string stage, Action run) => Time(stage, () =>
        {
            run();
            return 0;
        });

        public T Time<T>(string stage, Func<T> run)
        {
            var watch = Stopwatch.StartNew();
            var result = run();
            _stages.Add(new MapStageTime { stage = stage, milliseconds = watch.ElapsedMilliseconds });
            return result;
        }
    }
}
