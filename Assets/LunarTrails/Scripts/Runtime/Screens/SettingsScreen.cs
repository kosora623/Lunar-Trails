using LunarTrails.Runtime.Settings;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>仮の設定画面。中身は SettingsPanel。Esc か Tab で開く前の画面に戻る。</summary>
    public sealed class SettingsScreen : GameScreenBase
    {
        private SettingsPanel _panel;

        protected override void Awake()
        {
            base.Awake();
            var player = Director.Player;
            _panel = new SettingsPanel(() => player.CurrentSettings, player.ApplySettings);
        }

        private void OnEnable()
        {
            _panel?.ResetSelection();
        }

        private void Update()
        {
            var keyboard = ReadableKeyboard();
            if (keyboard == null) return;
            if (_panel.HandleInput(keyboard)) Director.Back();
        }

        private void OnGUI()
        {
            PlainGui.Text(
                "<size=32>設定</size>\n\n" +
                SettingsPanel.HelpText + "\n" +
                $"保存先: {SettingsStore.FilePath}\n\n" +
                _panel.Text());
        }
    }
}
