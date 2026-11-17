namespace LunarTrails.Runtime.Audio
{
    /// <summary>
    /// 曲の再生位置を「今の時刻」として配る時計。ゲーム中の時刻はすべてここから取る。
    /// </summary>
    /// <remarks>
    /// 音声の仕組み（Unity 標準 / CRI ADX LE など）を差し替えるときは、
    /// このインターフェースを実装したクラスを新しく作り、PrototypePlayer に渡す時計を入れ替える。
    /// 判定や表示のコードは変えなくてよい。
    /// </remarks>
    public interface ISongClock
    {
        /// <summary>
        /// 曲の再生位置（秒）。音声ファイルの頭が 0。再生前はマイナスになりうる（カウントイン中）。
        /// </summary>
        double SongTime { get; }

        bool IsPlaying { get; }

        /// <summary>指定した秒数だけ待ってから再生を始める。</summary>
        void Play(double delaySeconds);

        void Stop();
    }
}
