using System;
using HyperRTS.Network.Session;
using NUnit.Framework;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Command-line session flags: what each start reads and which combinations fail loudly.</summary>
    public class LaunchOptionsTests
    {
        private static LaunchOptions Parse(string line) => LaunchOptions.Parse(line.Split(' '));

        [Test]
        public void ObserverClient_ReadsAddressPortAndObserve()
        {
            var options = Parse("game.exe -batchmode -connect 127.0.0.1 -port 8000 -observe");

            Assert.AreEqual(LaunchMode.Client, options.Mode);
            Assert.AreEqual("127.0.0.1", options.Address);
            Assert.AreEqual(8000, options.Port);
            Assert.IsTrue(options.Observe);
        }

        [Test]
        public void Server_ReadsItsEndAndReport()
        {
            var options = Parse("game.exe -server -scene Maps/A.unity -exit-on-end -time-limit 45.5 -report out.json");

            Assert.AreEqual(LaunchMode.Server, options.Mode);
            Assert.AreEqual(NetworkSession.DefaultPort, options.Port);
            Assert.AreEqual("Maps/A.unity", options.ScenePath);
            Assert.IsTrue(options.ExitOnEnd);
            Assert.AreEqual(45.5f, options.TimeLimitMinutes);
            Assert.AreEqual("out.json", options.ReportPath);
            Assert.IsTrue(options.EndsItself);
        }

        [Test]
        public void NoSessionFlag_StartsNothing()
        {
            var options = Parse("game.exe -screen-fullscreen 0");

            Assert.AreEqual(LaunchMode.None, options.Mode);
            Assert.IsNull(options.ScenePath);
            Assert.IsFalse(options.EndsItself);
        }

        [TestCase("game.exe -host -observe", TestName = "ObserveNeedsConnect")]
        [TestCase("game.exe -connect 1.2.3.4 -exit-on-end", TestName = "ExitOnEndNeedsServer")]
        [TestCase("game.exe -server -report out.json", TestName = "ReportNeedsAnEnd")]
        [TestCase("game.exe -server -host", TestName = "OnlyOneMode")]
        [TestCase("game.exe -server -port 0", TestName = "PortZeroIsRejected")]
        [TestCase("game.exe -server -port lots", TestName = "PortMustBeANumber")]
        [TestCase("game.exe -server -time-limit -5", TestName = "TimeLimitMustBePositive")]
        [TestCase("game.exe -connect", TestName = "ConnectNeedsAnAddress")]
        public void BadFlags_Throw(string line) => Assert.Throws<ArgumentException>(() => Parse(line));
    }
}
