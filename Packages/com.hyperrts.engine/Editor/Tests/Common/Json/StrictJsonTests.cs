using System;
using HyperRTS.Editor.Common.Json;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Common.Json
{
    /// <summary>Hand-written JSON is read whole or rejected with an error that names what was read and the bad key.</summary>
    public class StrictJsonTests
    {
        [Test]
        public void ReadsPublicFieldsAndNestedObjects()
        {
            var read = StrictJson.Read<Outer>("{\"count\":3,\"inner\":{\"label\":\"a\"}}", "test", false);

            Assert.AreEqual(3, read.count);
            Assert.AreEqual("a", read.inner.label);
        }

        [Test]
        public void UnknownKeyThrowsNamingTheSourceAndTheKeysPath()
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => StrictJson.Read<Outer>("{\"count\":1,\"inner\":{\"lable\":\"a\"}}", "test spec", false));

            StringAssert.Contains("test spec", error.Message);
            StringAssert.Contains("inner.lable", error.Message);
        }

        [Test]
        public void ValueOfTheWrongShapeThrows()
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => StrictJson.Read<Outer>("{\"count\":\"many\"}", "test spec", false));

            StringAssert.Contains("test spec", error.Message);
            StringAssert.Contains("count", error.Message);
        }

        [Test]
        public void KeysThatNameAPropertyInsteadOfAFieldAreUnknown() =>
            Assert.Throws<InvalidOperationException>(() => StrictJson.Read<Outer>("{\"Doubled\":2}", "test", false));

        [Test]
        public void AnEmptyValueIsNotAnObject()
        {
            var error = Assert.Throws<InvalidOperationException>(() => StrictJson.Read<Outer>("null", "test spec", false));

            StringAssert.Contains("test spec must be a JSON object", error.Message);
        }

        [Test]
        public void ValuesAreOptionalUnlessRequired()
        {
            Assert.AreEqual(0, StrictJson.Read<Outer>("{}", "test", false).count);

            var error = Assert.Throws<InvalidOperationException>(() => StrictJson.Read<Outer>("{}", "test", true));
            StringAssert.Contains("count", error.Message);
        }

        [Test]
        public void ANullableFieldStaysOptionalWhenValuesAreRequired()
        {
            var read = StrictJson.Read<WithNullable>("{\"required\":1}", "test", true);

            Assert.AreEqual(1, read.required);
            Assert.IsNull(read.optional);
        }

        [Serializable]
        public class Inner
        {
            public string label;
        }

        [Serializable]
        public class Outer
        {
            public int count;
            public Inner inner;

            public int Doubled => count * 2;
        }

        [Serializable]
        public class WithNullable
        {
            public int required;
            public int? optional;
        }
    }
}
