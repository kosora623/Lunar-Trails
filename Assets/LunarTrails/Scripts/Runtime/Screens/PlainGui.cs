using UnityEngine;

namespace LunarTrails.Runtime.Screens
{
    /// <summary>
    /// 仮の画面で使う、飾りのない文字表示。デザインが決まったら本番の画面に置き換えるので、ここは最小限にしておく。
    /// </summary>
    public static class PlainGui
    {
        private static GUIStyle _text;

        /// <summary>画面の左上から、余白を空けて文字を出す（リッチテキスト可）。</summary>
        public static void Text(string text)
        {
            if (_text == null) _text = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            GUI.Label(new Rect(40, 40, Screen.width - 80, Screen.height - 80), text, _text);
        }
    }
}
