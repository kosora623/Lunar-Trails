using UnityEngine;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>仮のタイトル画面。Enter で選曲へ、Tab で設定、Esc でゲームを終わる。</summary>
    public sealed class TitleScreen : GameScreenBase
    {
        private void Update()
        {
            var keyboard = ReadableKeyboard();
            if (keyboard == null) return;

            if (EnterPressed(keyboard)) Director.GoToSongSelect();
            else if (keyboard.tabKey.wasPressedThisFrame) Director.OpenSettings();
            else if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit(); // エディタでは何も起きない
        }

        private void OnGUI()
        {
            PlainGui.Text(
                "<size=48>Lunar Trails</size>\n\n\n" +
                "Enter: はじめる\n" +
                "Tab: 設定\n" +
                "Esc: 終わる");
        }
    }
}
