using UnityEngine;

namespace LunarTrails.Runtime.Audio
{
    /// <summary>
    /// Unity 標準の AudioSource で曲を鳴らす時計。
    /// </summary>
    /// <remarks>
    /// Time.time（フレームの時刻）ではなく、音声側の時計 AudioSettings.dspTime を基準にする。
    /// フレームの時刻で数えると、曲と少しずつずれていくため。
    ///
    /// 既知の課題：dspTime は音声バッファ単位でしか進まないので、フレームによっては同じ値が続く。
    /// ノーツの表示がカクつくようなら、フレームの経過時間で補間する処理をここに足す。
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioSourceSongClock : MonoBehaviour, ISongClock
    {
        [SerializeField] private AudioSource _source;

        private double _startDspTime;
        private bool _scheduled;

        public double SongTime => _scheduled ? AudioSettings.dspTime - _startDspTime : 0.0;

        public bool IsPlaying => _scheduled;

        private void Reset()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        private void Awake()
        {
            if (_source == null) _source = GetComponent<AudioSource>();
        }

        public void Play(double delaySeconds)
        {
            _startDspTime = AudioSettings.dspTime + delaySeconds;
            _source.PlayScheduled(_startDspTime);
            _scheduled = true;
        }

        public void Stop()
        {
            _source.Stop();
            _scheduled = false;
        }
    }
}
