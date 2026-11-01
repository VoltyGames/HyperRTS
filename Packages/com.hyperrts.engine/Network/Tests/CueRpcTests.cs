using HyperRTS.Network.Cues;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Network.Tests
{
    /// <summary>Cue events batch into RPCs under the capacity, and a barrage drops fire first, then impacts.</summary>
    public class CueRpcTests
    {
        [Test]
        public void Fill_MakesRPCsOf48_AndDropsPastTheLimit()
        {
            using var cues = new NativeArray<CueEvent>(200, Allocator.Temp);
            using var rpcs = new NativeList<CueRpc>(Allocator.Temp);

            CueRpc.Fill(cues, 2, rpcs);
            Assert.AreEqual(2, rpcs.Length);
            Assert.AreEqual(CueRpc.Capacity, rpcs[0].Cues.Length);
            Assert.AreEqual(CueRpc.Capacity, rpcs[1].Cues.Length);

            rpcs.Clear();
            CueRpc.Fill(cues, CueSendSystem.MaxRpcsPerTick, rpcs);
            Assert.AreEqual(CueSendSystem.MaxRpcsPerTick, rpcs.Length, "a barrage drops the rest");
        }

        [Test]
        public void Fill_PastTheLimit_DropsFireThenImpacts_AndKeepsArrivalOrder()
        {
            var budget = CueSendSystem.MaxRpcsPerTick * CueRpc.Capacity;
            using var cues = new NativeList<CueEvent>(Allocator.Temp);
            Add(cues, CueSlot.Fire, budget);
            Add(cues, CueSlot.Impact, 20);
            Add(cues, CueSlot.Death, 5);
            Add(cues, CueSlot.Attack, 5);
            using var rpcs = new NativeList<CueRpc>(Allocator.Temp);

            CueRpc.Fill(cues.AsArray(), CueSendSystem.MaxRpcsPerTick, rpcs);

            using var sent = new NativeList<CueSlot>(Allocator.Temp);
            foreach (var rpc in rpcs)
            {
                foreach (var packed in rpc.Cues)
                {
                    sent.Add(packed.Unpack().Slot);
                }
            }

            Assert.AreEqual(budget, sent.Length);
            Assert.AreEqual(budget - 30, Count(sent, CueSlot.Fire), "fire makes room");
            Assert.AreEqual(20, Count(sent, CueSlot.Impact));
            Assert.AreEqual(5, Count(sent, CueSlot.Death));
            Assert.AreEqual(5, Count(sent, CueSlot.Attack));
            Assert.AreEqual(CueSlot.Fire, sent[0]);
            Assert.AreEqual(CueSlot.Attack, sent[budget - 1], "arrival order is kept");
        }

        private static void Add(NativeList<CueEvent> cues, CueSlot slot, int count)
        {
            for (var i = 0; i < count; i++)
            {
                cues.Add(new CueEvent { TypeId = 1, Slot = slot, Direction = new float3(0f, 0f, 1f) });
            }
        }

        private static int Count(NativeList<CueSlot> slots, CueSlot slot)
        {
            var count = 0;
            foreach (var each in slots)
            {
                count += each == slot ? 1 : 0;
            }

            return count;
        }
    }
}
