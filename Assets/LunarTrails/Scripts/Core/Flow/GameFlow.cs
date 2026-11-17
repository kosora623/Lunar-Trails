using System;

namespace LunarTrails.Core.Flow
{
    /// <summary>ゲームの画面。</summary>
    public enum GameScreen
    {
        Title,
        SongSelect,
        Settings,
        Play,
        Result,
    }

    /// <summary>
    /// 画面の流れ（タイトル → 選曲 → プレイ → リザルト、と設定）を決める。
    /// どの画面からどの画面へ行けるかをここにまとめ、ありえない移り方をしたらすぐ気づけるようにする。
    /// </summary>
    /// <remarks>
    /// 行ける先：
    /// - タイトル → 選曲、設定
    /// - 選曲 → タイトル、設定、プレイ
    /// - 設定 → 開く前の画面（タイトルか選曲）
    /// - プレイ → リザルト、選曲（途中でやめたとき）、プレイ（やり直し）
    /// - リザルト → 選曲、プレイ（もう一度）
    /// 画面の見た目や操作は Runtime 側の画面ごとの部品が受け持ち、このクラスは「今どの画面か」だけを持つ。
    /// </remarks>
    public sealed class GameFlow
    {
        public GameScreen Current { get; private set; }

        /// <summary>設定を閉じたときに戻る画面</summary>
        private GameScreen _beforeSettings = GameScreen.Title;

        /// <summary>画面が変わったときに呼ばれる（前の画面, 新しい画面）。</summary>
        public event Action<GameScreen, GameScreen> Changed;

        public GameFlow(GameScreen start = GameScreen.Title)
        {
            Current = start;
        }

        /// <summary>今の画面から next へ行けるか。</summary>
        public bool CanGo(GameScreen next)
        {
            switch (Current)
            {
                case GameScreen.Title:
                    return next == GameScreen.SongSelect || next == GameScreen.Settings;
                case GameScreen.SongSelect:
                    return next == GameScreen.Title || next == GameScreen.Settings || next == GameScreen.Play;
                case GameScreen.Settings:
                    return next == _beforeSettings;
                case GameScreen.Play:
                    return next == GameScreen.Result || next == GameScreen.SongSelect || next == GameScreen.Play;
                case GameScreen.Result:
                    return next == GameScreen.SongSelect || next == GameScreen.Play;
                default:
                    return false;
            }
        }

        /// <summary>next の画面へ移る。行けない画面なら InvalidOperationException。</summary>
        public void Go(GameScreen next)
        {
            if (!CanGo(next))
                throw new InvalidOperationException($"{Current} から {next} へは移れません");

            var previous = Current;
            if (next == GameScreen.Settings) _beforeSettings = previous;
            Current = next;
            Changed?.Invoke(previous, next);
        }

        /// <summary>
        /// 「戻る」（Esc）で行く画面。タイトルでは戻る先がないので null。
        /// </summary>
        public GameScreen? BackTarget
        {
            get
            {
                switch (Current)
                {
                    case GameScreen.Settings: return _beforeSettings;
                    case GameScreen.SongSelect: return GameScreen.Title;
                    case GameScreen.Play: return GameScreen.SongSelect;
                    case GameScreen.Result: return GameScreen.SongSelect;
                    default: return null;
                }
            }
        }

        /// <summary>「戻る」の画面へ移る。戻る先がなければ何もせず false。</summary>
        public bool Back()
        {
            var target = BackTarget;
            if (!target.HasValue) return false;
            Go(target.Value);
            return true;
        }
    }
}
