using System;
using HyperRTS.Editor.Hub;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Tools ▸ Play: cheats, turning the Play-mode scene into a networked match, bot matches and replays.</summary>
    public class PlayPage : HubPage
    {
        private const long PollMilliseconds = 250;

        private HelpBox _hint;
        private CheatsSection _cheats;
        private Button _host;
        private Button _join;
        private Button _server;
        private Button _stop;
        private Label _traffic;
        private Button _runBots;
        private Button _stopBots;
        private Label _botStatus;
        private readonly BotMatches.Settings _bots = new();
        private Button _save;
        private Button _play;
        private IVisualElementScheduledItem _poll;

        public override string Title => "Play";

        public override int Order => 40;

        public override VisualElement Build()
        {
            var scroll = new ScrollView();
            scroll.contentContainer.AddToClassList("hrts-page__body");
            _hint = new HelpBox("Enter Play mode to use cheats, networking and recording.", HelpBoxMessageType.Info);
            _hint.AddToClassList("hrts-hint");
            scroll.Add(_hint);

            _cheats = new CheatsSection();
            scroll.Add(_cheats.Root);
            scroll.Add(NetworkSection());
            scroll.Add(BotSection());
            scroll.Add(ReplaySection());

            _poll = scroll.schedule.Execute(Poll).Every(PollMilliseconds);
            Poll();
            return scroll;
        }

        public override void Release()
        {
            _poll?.Pause();
            _poll = null;
            _cheats = null;
        }

        private VisualElement NetworkSection()
        {
            var section = HubSections.Section("Network");
            _host = new Button(() => NetworkSession.StartHost()) { text = "Host" };
            _join = new Button(() => NetworkSession.StartClient("127.0.0.1")) { text = "Join Localhost" };
            _server = new Button(() => NetworkSession.StartServer()) { text = "Dedicated Server" };
            _stop = new Button(NetworkSession.Stop) { text = "Stop" };
            section.Add(HubSections.Buttons(_host, _join, _server, _stop));
            _traffic = new Label();
            _traffic.AddToClassList("hrts-note");
            section.Add(_traffic);
            return section;
        }

        private VisualElement BotSection()
        {
            var section = HubSections.Section("Bot matches");
            var matches = new IntegerField("Matches") { value = _bots.matches };
            matches.RegisterValueChangedCallback(change => _bots.matches = Math.Max(1, change.newValue));
            var players = new IntegerField("Players") { value = _bots.players };
            players.RegisterValueChangedCallback(change => _bots.players = Math.Max(2, change.newValue));
            var difficulty = new EnumField("Difficulty", _bots.difficulty);
            difficulty.RegisterValueChangedCallback(change => _bots.difficulty = (AIDifficulty)change.newValue);
            var speed = new FloatField("Game speed") { value = _bots.speed };
            speed.RegisterValueChangedCallback(change => _bots.speed = Math.Max(1f, change.newValue));
            var limit = new FloatField("Time limit (min)") { value = _bots.timeLimitMinutes };
            limit.RegisterValueChangedCallback(change => _bots.timeLimitMinutes = Math.Max(1f, change.newValue));
            section.Add(matches);
            section.Add(players);
            section.Add(difficulty);
            section.Add(speed);
            section.Add(limit);

            _runBots = new Button(() => BotMatches.Start(_bots)) { text = "Run" };
            _stopBots = new Button(BotMatches.Stop) { text = "Stop" };
            section.Add(HubSections.Buttons(_runBots, _stopBots));
            _botStatus = new Label();
            _botStatus.AddToClassList("hrts-note");
            section.Add(_botStatus);
            return section;
        }

        private VisualElement ReplaySection()
        {
            var section = HubSections.Section("Replays");
            _save = new Button(ReplayPlayback.SaveRecording) { text = "Save Recording..." };
            _play = new Button(ReplayPlayback.PlayReplay) { text = "Play Replay..." };
            section.Add(HubSections.Buttons(_save, _play));
            return section;
        }

        private void Poll()
        {
            var playing = Application.isPlaying;
            _hint.style.display = playing ? DisplayStyle.None : DisplayStyle.Flex;
            _cheats.Poll();

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

            _save.SetEnabled(ReplayPlayback.CanSave);
            _play.SetEnabled(ReplayPlayback.CanPlay);
        }

        private void ShowTraffic(bool running)
        {
            if (!running || !NetworkStress.TryRead(out var sample))
            {
                _traffic.style.display = DisplayStyle.None;
                return;
            }

            _traffic.text = sample.ToString();
            _traffic.style.display = DisplayStyle.Flex;
        }
    }
}
