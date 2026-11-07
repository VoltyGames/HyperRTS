using System;

namespace HyperRTS.Editor.Hub
{
    /// <summary>The count pill on a page's sidebar entry: red when any of the count is an error, amber otherwise.</summary>
    public readonly struct PageBadge : IEquatable<PageBadge>
    {
        public static readonly PageBadge None = default;

        public PageBadge(int count, bool hasErrors)
        {
            Count = count;
            HasErrors = hasErrors;
        }

        public int Count { get; }

        public bool HasErrors { get; }

        public bool IsNone => Count == 0;

        public bool Equals(PageBadge other) => Count == other.Count && HasErrors == other.HasErrors;

        public override bool Equals(object other) => other is PageBadge badge && Equals(badge);

        public override int GetHashCode() => HashCode.Combine(Count, HasErrors);
    }
}
