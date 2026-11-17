using LunarTrails.Core.Charts;
using UnityEngine;

namespace LunarTrails.Runtime.Charts
{
    /// <summary>
    /// 譜面 JSON を読み込んで Chart を作る。JSON の解析には Unity 標準の JsonUtility を使う。
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

        public static Chart FromTextAsset(TextAsset asset) => FromJson(asset.text);
    }
}
