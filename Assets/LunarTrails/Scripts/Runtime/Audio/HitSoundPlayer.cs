using UnityEngine;

namespace LunarTrails.Runtime.Audio
{
    /// <summary>
    /// 叩いたときの音（短いクリック音）を鳴らす。音のファイルは使わず、コードで作った音を使う。
    /// </summary>
    /// <remarks>
    /// オートプレイでは、ノーツの時刻ちょうどに鳴るよう PlayScheduled で予約する。
    /// フレームのタイミングで鳴らすと最大 1 フレーム分ずれて、音ズレの確認に使えないため。
    /// 正式な効果音ができたら、_clip を差し替える。
    /// </remarks>
    public sealed class HitSoundPlayer : MonoBehaviour
    {
        private const int VoiceCount = 8;
        private const int SampleRate = 44100;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.6f;

        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioClip _clip;

        private void Awake()
        {
            _clip = CreateClickClip();
            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.clip = _clip;
                _voices[i] = source;
            }
        }

        /// <summary>今すぐ鳴らす（手で叩いたとき用）。</summary>
        public void PlayNow()
        {
            var voice = NextVoice();
            voice.volume = _volume;
            voice.Play();
        }

        /// <summary>AudioSettings.dspTime の指定時刻に鳴らす予約をする（オートプレイ用）。</summary>
        public void PlayAt(double dspTime)
        {
            var voice = NextVoice();
            voice.volume = _volume;
            voice.PlayScheduled(dspTime);
        }

        public void StopAll()
        {
            foreach (var voice in _voices) voice.Stop();
        }

        private AudioSource NextVoice()
        {
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            return voice;
        }

        // 30ms の短い「コッ」という音。高めの音をすぐに小さくしていく。
        private static AudioClip CreateClickClip()
        {
            int length = SampleRate * 30 / 1000;
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = System.Math.Exp(-t * 150.0);
                samples[i] = (float)(System.Math.Sin(2 * System.Math.PI * 1800.0 * t) * envelope);
            }
            var clip = AudioClip.Create("HitClick", length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
