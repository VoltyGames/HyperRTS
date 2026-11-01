using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: HyperRTS.Editor.Maps.Tests.DestroyFixtureItems]

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>Destroys the library items <see cref="MapFixture"/> made once each test of the assembly finishes.</summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class DestroyFixtureItemsAttribute : Attribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Test;

        public void BeforeTest(ITest test)
        {
        }

        public void AfterTest(ITest test) => MapFixture.DestroyItems();
    }
}
