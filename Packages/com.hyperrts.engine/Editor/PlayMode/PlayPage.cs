using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.PlayMode.Launch;
using HyperRTS.Network.Session;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>The Tools window Play page: the launcher outside Play mode; network, soak tests and replays in it.</summary>
    public class PlayPage : HubPage
    {
        private const long PollMilliseconds = 250;

        private VisualElement _launch;
        private VisualElement _network;
        private VisualElement _soak;
        private Button _host;
        private Button _join;
        private Button _server;
        private Button _stop;
        private Label _traffic;
        private Button _runBots;
        private Button _stopBots;
        private Label _botStatus;
        private VisualElement _results;
        private int _shownResults = -1;
        private Button _save;
        private VisualElement _replays;
        private Label _noReplays;
        private System.DateTime? _shownReplays;

        public override string Title => "Play";

        public override string Glyph => "play";

        public override int Order => 40;

        public override VisualElement Build()
        {
            var root = Layout().Instantiate();
            EditorState.Bind(root);
            _launch = root.Require<VisualElement>("launch");
            new LaunchSection(_launch, EditorAssets.Layout(typeof(LaunchSection), "SlotRow"));
            _network = root.Require<VisualElement>("network");
            _soak = root.Require<VisualElement>("soak");
            BindNetwork(root);
            BindBots(root);
            _save = root.Require<Button>("save");
            _save.clicked += ReplayPlayback.SaveRecording;
            _replays = root.Require<VisualElement>("replays");
            _noReplays = root.Require<Label>("no-replays");

            root.schedule.Execute(Poll).Every(PollMilliseconds);
            Poll();
            return root;
        }

        private void BindNetwork(VisualElement root)
        {
            _host = root.Require<Button>("host");
            _host.clicked += () => NetworkSession.StartHost();
            _join = root.Require<Button>("join");
            _join.clicked += () => NetworkSession.StartClient("127.0.0.1");
            _server = root.Require<Button>("server");
            _server.clicked += () => NetworkSession.StartServer();
            _stop = root.Require<Button>("stop");
            _stop.clicked += NetworkSession.Stop;
            _traffic = root.Require<Label>("traffic");
        }

        private void BindBots(VisualElement root)
        {
            _runBots = root.Require<Button>("run-bots");
            _runBots.clicked += () => BotMatches.Start(EditorState.instance.bots);
            _stopBots = root.Require<Button>("stop-bots");
            _stopBots.clicked += BotMatches.Stop;
            _botStatus = root.Require<Label>("bot-status");
            _results = root.Require<VisualElement>("results");
        }

        private void Poll()
        {
            var playing = Application.isPlaying;
            _launch.SetHidden(playing);
            _network.SetHidden(!playing);
            _soak.SetHidden(!playing);

            var running = NetworkSession.IsRunning;
            var canStart = playing && !running;
            _host.SetEnabled(canStart);
            _join.SetEnabled(canStart);
            _server.SetEnabled(canStart);
            _stop.SetEnabled(playing && running);
            ShowTraffic(running);
            _runBots.SetEnabled(playing && !running && !BotMatches.IsRunning);
            _stopBots.SetEnabled(BotMatches.IsRunning);
            _botStatus.text = BotMatches.Status;
            ShowResults();

            _save.SetEnabled(ReplayPlayback.CanSave);
            ShowReplays();
        }

        private void ShowTraffic(bool running)
        {
            if (!running)
            {
                _traffic.text = "Single player: no network session.";
                return;
            }

            var status = NetworkSession.Status.ToString();
            _traffic.text = NetworkStress.TryRead(out var sample) ? $"{status}. {sample}" : status;
        }

        private void ShowResults()
        {
            var results = BotMatches.Results;
            if (results.Count == _shownResults)
            {
                return;
            }

            _shownResults = results.Count;
            _results.Clear();
            if (results.Count == 0)
            {
                return;
            }

            _results.Add(Row(true, "Run", "Match", "Minutes", "Winner", "Sides"));
            foreach (var result in Enumerable.Reverse(results))
            {
                var time = result.run.Length > 11 ? result.run.Substring(11) : result.run;
                _results.Add(Row(false, time, result.match.ToString(), result.minutes.ToString("F1"), result.winner,
                    result.sides));
            }
        }

        private static VisualElement Row(bool head, string run, string match, string minutes, string winner, string sides)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-play__row");
            row.EnableInClassList("hrts-play__head", head);
            foreach (var text in new[] { run, match, minutes })
            {
                var cell = new Label(text);
                cell.AddToClassList("hrts-play__cell");
                row.Add(cell);
            }

            foreach (var text in new[] { winner, sides })
            {
                var cell = new Label(text);
                cell.AddToClassList("hrts-play__wide");
                row.Add(cell);
            }

            return row;
        }

        private void ShowReplays()
        {
            // The page polls, so the folder is only listed again once a recording is added or removed.
            var changed = ReplayPlayback.RecordingsChanged;
            if (changed != _shownReplays)
            {
                _shownReplays = changed;
                var recent = ReplayPlayback.RecentRecordings();
                _noReplays.SetHidden(recent.Count > 0);
                _replays.Clear();
                recent.ForEach(path => _replays.Add(ReplayRow(path)));
            }

            _replays.Query<Button>("play-replay").ForEach(button => button.SetEnabled(ReplayPlayback.CanPlay));
        }

        private static VisualElement ReplayRow(string path)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-play__row");
            var name = new Label(Path.GetFileNameWithoutExtension(path)) { tooltip = path };
            name.AddToClassList("hrts-play__wide");
            row.Add(name);
            row.Add(new Button(() => ReplayPlayback.PlayReplay(path)) { name = "play-replay", text = "Play" });
            row.Add(new Button(() => EditorUtility.RevealInFinder(path)) { text = "Reveal" });
            return row;
        }
    }
}
