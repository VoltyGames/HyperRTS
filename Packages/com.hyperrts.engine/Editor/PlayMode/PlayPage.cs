using System;
using HyperRTS.Editor.Common;
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
            var root = EditorAssets.Layout(typeof(PlayPage)).Instantiate();
            _hint = root.Require<HelpBox>("hint");
            _cheats = new CheatsSection(root.Require<VisualElement>("cheats"));
            BindNetwork(root);
            BindBots(root);
            _save = root.Require<Button>("save");
            _save.clicked += ReplayPlayback.SaveRecording;
            _play = root.Require<Button>("play");
            _play.clicked += ReplayPlayback.PlayReplay;

            _poll = root.schedule.Execute(Poll).Every(PollMilliseconds);
            Poll();
            return root;
        }

        public override void Release()
        {
            _poll?.Pause();
            _poll = null;
            _cheats = null;
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
            var matches = root.Require<IntegerField>("bot-matches");
            matches.value = _bots.matches;
            matches.RegisterValueChangedCallback(change => _bots.matches = Math.Max(1, change.newValue));
            var players = root.Require<IntegerField>("bot-players");
            players.value = _bots.players;
            players.RegisterValueChangedCallback(change => _bots.players = Math.Max(2, change.newValue));
            var difficulty = root.Require<EnumField>("bot-difficulty");
            difficulty.Init(_bots.difficulty);
            difficulty.RegisterValueChangedCallback(change => _bots.difficulty = (AIDifficulty)change.newValue);
            var speed = root.Require<FloatField>("bot-speed");
            speed.value = _bots.speed;
            speed.RegisterValueChangedCallback(change => _bots.speed = Math.Max(1f, change.newValue));
            var limit = root.Require<FloatField>("bot-limit");
            limit.value = _bots.timeLimitMinutes;
            limit.RegisterValueChangedCallback(change => _bots.timeLimitMinutes = Math.Max(1f, change.newValue));

            _runBots = root.Require<Button>("run-bots");
            _runBots.clicked += () => BotMatches.Start(_bots);
            _stopBots = root.Require<Button>("stop-bots");
            _stopBots.clicked += BotMatches.Stop;
            _botStatus = root.Require<Label>("bot-status");
        }

        private void Poll()
        {
            var playing = Application.isPlaying;
            _hint.EnableInClassList("hrts-hidden", playing);
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
                _traffic.AddToClassList("hrts-hidden");
                return;
            }

            _traffic.text = sample.ToString();
            _traffic.RemoveFromClassList("hrts-hidden");
        }
    }
}
