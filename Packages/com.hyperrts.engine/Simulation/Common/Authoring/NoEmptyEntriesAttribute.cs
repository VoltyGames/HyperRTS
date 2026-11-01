using System;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>
    /// A list whose empty entries the baker skips. An entry is empty when it is missing or, given <see cref="Key"/>,
    /// when that field of it is. Validation offers to remove them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NoEmptyEntriesAttribute : Attribute
    {
        /// <summary>The entry field that must be set, such as a cost entry's resource type.</summary>
        public string Key { get; set; }
    }
}
