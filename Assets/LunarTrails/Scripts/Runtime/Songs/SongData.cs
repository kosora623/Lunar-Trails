using UnityEngine;

namespace LunarTrails.Runtime.Songs
{
    /// <summary>
    /// 1 曲分の情報（曲名・音声・ジャケット・譜面）。Unity のアセットとして作る。
    /// </summary>
    /// <remarks>
    /// 作り方：Project ウィンドウで右クリック → Create → Lunar Trails → Song。
    /// できたアセットに曲名や音声、譜面（.json か .sus、難易度ごとに 1 つ）を入れ、SongLibrary に並べる。
    /// 難易度（stl / ast …）とレベルは譜面ファイルの中に書いてあるものを使うので、ここでは入れない。
    /// </remarks>
    [CreateAssetMenu(menuName = "Lunar Trails/Song", fileName = "Song")]
    public sealed class SongData : ScriptableObject
    {
        [Tooltip("記録の保存に使う、曲ごとに違う ID（英数字）。空ならアセットの名前を使う。あとから変えると自己ベストが引き継がれないので注意")]
        public string id;

        public string title;
        public string artist;

        [Tooltip("選曲画面に出す BPM の表記（例：「150」「120-180」）")]
        public string bpmText;

        [Tooltip("ジャケット画像（空でもよい）")]
        public Texture2D jacket;

        [Tooltip("曲の音声。空でも遊べる（叩く音だけで確認できる）")]
        public AudioClip audio;

        [Tooltip("選曲画面で流す試聴の開始位置（秒）")]
        public float previewStart;

        [Tooltip("試聴の長さ（秒）。この長さで頭に戻って繰り返す")]
        public float previewLength = 15f;

        [Tooltip("この曲の譜面（難易度ごとに 1 つ）")]
        public TextAsset[] charts = new TextAsset[0];

        /// <summary>記録のキーに使う ID。id が空ならアセット名。</summary>
        public string SongId => string.IsNullOrEmpty(id) ? name : id;
    }
}
