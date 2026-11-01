using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Network.Session;
using HyperRTS.Presentation.HUD;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Tools ▸ Play: cheats, turning the Play-mode scene into a networked match, bot matches and replays.</summary>
    public class PlayPage : HubPage
    {
        private const long PollMilliseconds = 250;

        private PageShell _shell;
        private CheatsSection _cheats;
        private Button _host;
        private Button _join;
        private Button _server;
        private Button _stop;
        private Label _traffic;
        private Button _runBots;
        private Button _stopBots;
        private Label _botStatus;
        private Button _save;
        private Button _play;

        public override string Title => "Play";

        public override int Order => 40;

        public override VisualElement Build()
        {
            var root = Layout().Instantiate();
            HyperRTSEditorState.Bind(root);
            _shell = root.Require<PageShell>("shell");
            _cheats = new CheatsSection(root.Require<VisualElement>("cheats"));
            BindNetwork(root);
            BindBots(root);
            _save = root.Require<Button>("save");
            _save.clicked += ReplayPlayback.SaveRecording;
            _play = root.Require<Button>("play");
            _play.clicked += ReplayPlayback.PlayReplay;

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
            _runBots.clicked += () => BotMatches.Start(HyperRTSEditorState.instance.bots);
            _stopBots = root.Require<Button>("stop-bots");
            _stopBots.clicked += BotMatches.Stop;
            _botStatus = root.Require<Label>("bot-status");
        }

        private void Poll()
        {
            var playing = Application.isPlaying;
            _shell.ShowEmpty(!playing);
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
                _traffic.SetHidden(true);
                return;
            }

            _traffic.text = sample.ToString();
            _traffic.SetHidden(false);
        }
    }
}
