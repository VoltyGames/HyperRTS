using System;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>A rule's minimal broken case and a fragment of what the rule must report for it.</summary>
    internal readonly struct RuleCase
    {
        public readonly Type Rule;
        public readonly string Expected;
        public readonly Func<RuleFiresTests, object> Build;

        public RuleCase(Type rule, string expected, Func<RuleFiresTests, object> build)
        {
            Rule = rule;
            Expected = expected;
            Build = build;
        }
    }
}
