using System.Collections.Generic;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Charts.Sus;
using UnityEngine;

namespace LunarTrails.Runtime.Charts
{
    /// <summary>
    /// 譜面ファイルを読み込んで Chart を作る。
    /// JSON（Lunar Trails の形式）と SUS（Ched などが書き出す形式）のどちらも読める。中身の先頭で見分ける。
    /// JSON の解析には Unity 標準の JsonUtility を使う。
    /// </summary>
    public static class ChartLoader
    {
        /// <exception cref="ChartFormatException">譜面の中身がおかしいとき</exception>
        public static Chart FromJson(string json)
        {
            ChartData data;
            try
            {
                data = JsonUtility.FromJson<ChartData>(json);
            }
            catch (System.ArgumentException e)
            {
                throw new ChartFormatException($"JSON として読めません: {e.Message}");
            }
            return ChartBuilder.Build(data);
        }

        /// <summary>SUS を読む。使わないノーツを読み飛ばしたときは、その内容を warnings に入れる。</summary>
        /// <exception cref="ChartFormatException">譜面の中身がおかしいとき</exception>
        public static Chart FromSus(string sus, List<string> warnings = null) => SusConverter.ToChart(sus, warnings);

        /// <summary>中身が { で始まれば JSON、それ以外は SUS として読む。</summary>
        /// <exception cref="ChartFormatException">譜面の中身がおかしいとき</exception>
        public static Chart FromText(string text, string nameForLog = null)
        {
            if (IsJson(text)) return FromJson(text);

            var warnings = new List<string>();
            var chart = FromSus(text, warnings);
            foreach (var warning in warnings) Debug.LogWarning($"{nameForLog}: {warning}");
            return chart;
        }

        public static Chart FromTextAsset(TextAsset asset) => FromText(asset.text, asset.name);

        private static bool IsJson(string text) => text != null && text.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("{");
    }
}
