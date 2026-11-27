using System;

namespace HyperRTS.Editor.Tests.Validation;

/// <summary>A rule's minimal broken case and a fragment of what the rule must report for it.</summary>
internal readonly struct RuleCase(Type rule, string expected, Func<RuleFiresTests, object> build)
{
    public readonly Type Rule = rule;
    public readonly string Expected = expected;
    public readonly Func<RuleFiresTests, object> Build = build;
}
