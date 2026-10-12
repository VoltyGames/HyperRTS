using HyperRTS.Editor.Hub;
using HyperRTS.Network.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Tools ▸ Play: cheats, turning the Play-mode scene into a networked match, and replays.</summary>
    public class PlayPage : HubPage
    {
        private const long PollMilliseconds = 250;

        private HelpBox _hint;
        private CheatsSection _cheats;
        private Button _host;
        private Button _join;
        private Button _server;
        private Button _stop;
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

            _save.SetEnabled(ReplayPlayback.CanSave);
            _play.SetEnabled(ReplayPlayback.CanPlay);
        }
    }
}
