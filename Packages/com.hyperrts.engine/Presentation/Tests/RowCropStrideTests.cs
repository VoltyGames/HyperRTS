using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>Row crop GPU structs keep the byte layouts RowCrops.compute declares.</summary>
    public class RowCropStrideTests
    {
        [Test]
        public void Field_MatchesTheKernelsThirtyTwoBytes() => Assert.AreEqual(32, RowCropField.Stride);

        [Test]
        public void Job_MatchesTheKernelsTwentyEightBytes() => Assert.AreEqual(28, RowCropJob.Stride);
    }
}
