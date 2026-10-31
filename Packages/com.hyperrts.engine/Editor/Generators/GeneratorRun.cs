using System;

namespace HyperRTS.Editor.Generators
{
    /// <summary>One generator's last run: its local time and, when it failed, why.</summary>
    [Serializable]
    public sealed class GeneratorRun
    {
        public string generator;
        public string time;
        public string error;

        public bool Failed => !string.IsNullOrEmpty(error);

        public override string ToString() => Failed ? $"Failed {time}: {error}" : $"Ran {time}";
    }
}
