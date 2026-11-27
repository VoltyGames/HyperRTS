using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HyperRTS.Editor.Stages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HyperRTS.Editor.Tests.Stages;

/// <summary>The queue runs stages one at a time in the order added, and a failure drops only its own source's later stages.</summary>
public class StageQueueTests
{
    private readonly List<string> _ran = [];
    private readonly List<string> _finished = [];
    private StageQueue<string> _queue;
    private TaskCompletionSource<bool> _gate;

    [SetUp]
    public void CreateQueue()
    {
        _ran.Clear();
        _finished.Clear();
        _gate = new TaskCompletionSource<bool>();
        _queue = new StageQueue<string>(source => source);
        _queue.Finished += _finished.Add;
    }

    // The first stage waits on the gate, so what is added after it queues; opening the gate drains the rest in one call.
    private void Hold() => _queue.Add("held", new TestStage("hold", _ran, _ => _gate.Task));

    [Test]
    public void StagesRunInTheOrderAddedAcrossSources()
    {
        Hold();
        _queue.Add("a", new TestStage("one", _ran));
        _queue.Add("b", new TestStage("one", _ran));
        _queue.Add("a", new TestStage("two", _ran));
        Assert.IsTrue(_queue.Busy);
        Assert.AreEqual("held", _queue.Running.Value.Source);

        _gate.SetResult(true);

        Assert.AreEqual(new[] { "hold@held", "one@a", "one@b", "two@a" }, _ran);
        Assert.AreEqual(new[] { "held", "a", "b", "a" }, _finished);
        Assert.IsFalse(_queue.Busy);
    }

    [Test]
    public void AStageAlreadyWaitingOrRunningForTheSourceIsNotAddedAgain()
    {
        Hold();
        _queue.Add("held", new TestStage("hold", _ran));
        _queue.Add("a", new TestStage("one", _ran));
        _queue.Add("a", new TestStage("one", _ran));

        _gate.SetResult(true);

        Assert.AreEqual(new[] { "hold@held", "one@a" }, _ran);
    }

    [Test]
    public void AFailedStageDropsItsSourcesLaterStagesButNotOthers()
    {
        LogAssert.Expect(LogType.Exception, new Regex("boom"));
        Hold();
        _queue.Add("a", new TestStage("one", _ran, _ => throw new InvalidOperationException("boom")));
        _queue.Add("a", new TestStage("two", _ran));
        _queue.Add("b", new TestStage("one", _ran));

        _gate.SetResult(true);

        Assert.AreEqual(new[] { "hold@held", "one@a", "one@b" }, _ran);
        Assert.AreEqual(new[] { "held", "a", "b" }, _finished);
        Assert.IsFalse(_queue.Busy);
    }

    [Test]
    public void StopDropsWaitingStagesAndEndsTheRunningOne()
    {
        var stage = new TestStage("hold", _ran, _ => _gate.Task) { OnStop = () => _gate.SetResult(true) };
        _queue.Add("held", stage);
        _queue.Add("a", new TestStage("one", _ran));

        _queue.Stop();

        Assert.AreEqual(new[] { "hold@held" }, _ran);
        Assert.AreEqual(new[] { "held" }, _finished);
        Assert.IsFalse(_queue.Busy);
    }

    private sealed class TestStage : Stage<string>
    {
        private readonly List<string> _ran;
        private readonly Func<string, Task> _run;

        public TestStage(string title, List<string> ran, Func<string, Task> run = null) : base(title)
        {
            _ran = ran;
            _run = run ?? (_ => Task.CompletedTask);
        }

        public Action OnStop { get; set; }

        public override StageState State(string source) => StageState.Done;

        public override Task Run(string source)
        {
            _ran.Add($"{Title}@{source}");
            return _run(source);
        }

        public override void Stop() => OnStop?.Invoke();
    }
}
