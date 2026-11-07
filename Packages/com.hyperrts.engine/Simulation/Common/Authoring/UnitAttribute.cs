using System;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>Names the unit of a number field, such as "m" or "m/s"; authoring inspectors draw it dim inside the field.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UnitAttribute : Attribute
    {
        public UnitAttribute(string unit)
        {
            Unit = unit;
        }

        public string Unit { get; }
    }
}
