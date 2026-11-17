using UnityEngine;

namespace LunarTrails.Runtime.Songs
{
    /// <summary>
    /// 選曲画面に並べる曲の一覧。並び順がそのまま選曲画面の順になる。
    /// </summary>
    /// <remarks>
    /// 作り方：Project ウィンドウで右クリック → Create → Lunar Trails → Song Library。
    /// できたアセットの Songs に SongData を並べ、GameDirector の Library に入れる。
    /// </remarks>
    [CreateAssetMenu(menuName = "Lunar Trails/Song Library", fileName = "SongLibrary")]
    public sealed class SongLibrary : ScriptableObject
    {
        public SongData[] songs = new SongData[0];
    }
}
