using System;
using System.Diagnostics;
using System.IO;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Watches a command-line server that ends itself: samples its cost each second of play, quits when the match
    /// ends or the time limit passes, and writes the <see cref="ServerReport"/>.
    /// </summary>
    internal sealed class ServerMonitor : MonoBehaviour
    {
        public const int EndedCode = 0;
        public const int TimedOutCode = 2;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly string _started = DateTime.UtcNow.ToString("o");
        private LaunchOptions _options;
        private ServerCostSampler _sampler;
        private World _world;
        private EntityQuery _matchState;
        private double _matchStart = -1;
        private double _nextSample;
        private int _errors;
        private bool _finished;

        public static void Begin(LaunchOptions options)
        {
            var host = new GameObject(nameof(ServerMonitor));
            DontDestroyOnLoad(host);
            host.AddComponent<ServerMonitor>()._options = options;
        }

        private void Awake() => Application.logMessageReceived += CountErrors;

        private void OnDestroy()
        {
            Application.logMessageReceived -= CountErrors;
            _sampler?.Dispose();
        }

        private void CountErrors(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                _errors++;
            }
        }

        private void Update()
        {
            var world = ClientServerBootstrap.ServerWorld;
            if (_finished || world == null || !world.IsCreated)
            {
                return;
            }

            // A query lives as long as its world, so it's made once per server world.
            if (world != _world)
            {
                _world = world;
                _matchState = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            }

            var hasState = _matchState.TryGetSingleton(out MatchState state);
            Sample(world, hasState && state.Started);
            var seconds = world.Time.ElapsedTime;
            if (_options.TimeLimitMinutes > 0f && seconds >= _options.TimeLimitMinutes * 60.0)
            {
                Finish(world, state, ServerReport.TimedOut, TimedOutCode);
                return;
            }

            if (_options.ExitOnEnd && hasState && state.Phase == MatchPhase.Ended)
            {
                Finish(world, state, ServerReport.Ended, EndedCode);
            }
        }

        // Cost counts from the match start: loading the map is not what a match costs to run.
        private void Sample(World world, bool started)
        {
            if (!started)
            {
                return;
            }

            var now = _clock.Elapsed.TotalSeconds;
            if (_sampler == null)
            {
                _sampler = new ServerCostSampler(world);
                _matchStart = world.Time.ElapsedTime;
                _nextSample = now + 1.0;
                return;
            }

            if (now >= _nextSample)
            {
                _sampler.Record(world);
                _nextSample += 1.0;
            }
        }

        private void Finish(World world, in MatchState state, string outcome, int exitCode)
        {
            _finished = true;
            var report = Report(world, state, outcome, exitCode);
            if (_options.ReportPath != null && !TryWrite(report))
            {
                exitCode = NetworkLauncher.ErrorCode;
            }

            Debug.Log($"[HyperRTS] Server {outcome} after {report.serverMinutes:F1} min; exit code {exitCode}.");
            Application.Quit(exitCode);
        }

        private ServerReport Report(World world, in MatchState state, string outcome, int exitCode)
        {
            var seconds = world.Time.ElapsedTime;
            var report = new ServerReport
            {
                scene = SceneManager.GetActiveScene().path,
                started = _started,
                outcome = outcome,
                exitCode = exitCode,
                serverMinutes = (float)(seconds / 60.0),
                matchMinutes = _matchStart < 0 ? 0f : (float)((seconds - _matchStart) / 60.0),
                winningTeam = state.Phase == MatchPhase.Ended ? state.WinningTeam : 0,
                errors = _errors,
            };
            report.players.AddRange(ServerReport.PlayerResult.Read(world.EntityManager));
            _sampler?.Summarize(report);
            return report;
        }

        private bool TryWrite(ServerReport report)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(_options.ReportPath));
                Directory.CreateDirectory(directory);
                File.WriteAllText(_options.ReportPath, JsonUtility.ToJson(report, true));
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.LogError($"[HyperRTS] Could not write the server report to {_options.ReportPath}: {exception.Message}");
                return false;
            }
        }
    }
}
