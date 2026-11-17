using System;
using System.Collections.Generic;
using LunarTrails.Core.Scoring;

namespace LunarTrails.Core.Records
{
    /// <summary>1 譜面ぶんの自己ベスト。</summary>
    [Serializable]
    public sealed class PlayRecord
    {
        /// <summary>どの譜面の記録か（RecordBook.Key で作る「曲の ID/難易度」）</summary>
        public string key;
        /// <summary>いちばん高い達成率（%）</summary>
        public double bestRate;
        /// <summary>いちばん良いランプ（達成率とは別に、一番良かったものを残す）</summary>
        public Lamp bestLamp;
        /// <summary>遊んだ回数（最後まで遊んだ回数。途中でやめたものは数えない）</summary>
        public int playCount;
    }

    /// <summary>記録を出したときに、何が更新されたか。リザルト画面の「NEW RECORD」表示などに使う。</summary>
    public readonly struct RecordUpdate
    {
        /// <summary>この譜面を初めて最後まで遊んだ</summary>
        public bool IsFirstPlay { get; }
        /// <summary>達成率が自己ベストを超えた（初めてのときも true）</summary>
        public bool IsNewBestRate { get; }
        /// <summary>ランプが前より良くなった（初めてのときも true）</summary>
        public bool IsNewBestLamp { get; }
        /// <summary>前の自己ベストの達成率。初めてなら null。</summary>
        public double? PreviousBestRate { get; }

        public RecordUpdate(bool isFirstPlay, bool isNewBestRate, bool isNewBestLamp, double? previousBestRate)
        {
            IsFirstPlay = isFirstPlay;
            IsNewBestRate = isNewBestRate;
            IsNewBestLamp = isNewBestLamp;
            PreviousBestRate = previousBestRate;
        }
    }

    /// <summary>
    /// 全譜面の自己ベストをまとめたもの。保存と読み込みは Runtime の RecordStore が JSON で行う。
    /// </summary>
    /// <remarks>
    /// JsonUtility で保存できるように、辞書ではなくリストで持つ（譜面の数は多くても数百なので、探すのは線形でよい）。
    /// オートプレイの結果は記録しない（呼ぶ側で Submit しない）。
    /// </remarks>
    [Serializable]
    public sealed class RecordBook
    {
        public List<PlayRecord> records = new List<PlayRecord>();

        /// <summary>記録のキー：「曲の ID/難易度」。難易度は stl / ast / lnr / nov / inv。</summary>
        public static string Key(string songId, string difficulty) => $"{songId}/{difficulty}";

        /// <summary>譜面の記録。まだ遊んでいなければ null。</summary>
        public PlayRecord Find(string key)
        {
            if (records == null) return null;
            foreach (var record in records)
            {
                if (record != null && record.key == key) return record;
            }
            return null;
        }

        /// <summary>最後まで遊んだ結果を記録する。</summary>
        public RecordUpdate Submit(string key, PlayResult result)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("記録のキーが空です", nameof(key));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (records == null) records = new List<PlayRecord>();

            var record = Find(key);
            if (record == null)
            {
                records.Add(new PlayRecord { key = key, bestRate = result.AchievementRate, bestLamp = result.Lamp, playCount = 1 });
                return new RecordUpdate(true, true, true, null);
            }

            double previous = record.bestRate;
            bool newRate = result.AchievementRate > record.bestRate;
            bool newLamp = result.Lamp > record.bestLamp;
            if (newRate) record.bestRate = result.AchievementRate;
            if (newLamp) record.bestLamp = result.Lamp;
            record.playCount++;
            return new RecordUpdate(false, newRate, newLamp, previous);
        }

        /// <summary>手で書き換えられた保存ファイルを読んだときのために、壊れた記録を取り除く。</summary>
        public void Sanitize()
        {
            if (records == null)
            {
                records = new List<PlayRecord>();
                return;
            }
            records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.key));
            foreach (var r in records)
            {
                if (double.IsNaN(r.bestRate) || r.bestRate < 0) r.bestRate = 0;
                if (r.bestRate > PlayResult.PerfectPlusRate) r.bestRate = PlayResult.PerfectPlusRate;
                if (!Enum.IsDefined(typeof(Lamp), r.bestLamp)) r.bestLamp = Lamp.Clear;
                if (r.playCount < 0) r.playCount = 0;
            }
        }
    }
}
