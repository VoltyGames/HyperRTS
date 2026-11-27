using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging;

/// <summary>Collects the console's errors and exceptions while it lives, one entry per message and first stack line.</summary>
internal sealed class ConsoleIssues : IDisposable
{
    private readonly Dictionary<string, StagingReport.Issue> _byKey = [];

    public ConsoleIssues() => Application.logMessageReceived += Receive;

    public IReadOnlyCollection<StagingReport.Issue> Issues => _byKey.Values;

    public void Dispose() => Application.logMessageReceived -= Receive;

    private void Receive(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Log || type == LogType.Warning)
        {
            return;
        }

        var stackLine = stackTrace.Split('\n').FirstOrDefault(line => line.Trim().Length > 0)?.Trim() ?? "";
        var key = message + "\n" + stackLine;
        if (_byKey.TryGetValue(key, out var issue))
        {
            issue.count++;
            return;
        }

        _byKey[key] = new StagingReport.Issue { message = message, stackLine = stackLine, count = 1 };
    }
}
