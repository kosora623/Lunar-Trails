using System.Collections.Generic;
using System.IO;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Charts.Sus;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace LunarTrails.Editor
{
    /// <summary>
    /// .sus ファイル（Ched などの譜面）を Unity にテキストとして取り込む。
    /// 取り込んだものは JSON の譜面と同じく、PrototypePlayer の Songs の譜面欄に入れられる。
    /// 取り込むときに一度読んでみて、読めないところや読み飛ばしたノーツがあれば Console に出す。
    /// </summary>
    [ScriptedImporter(1, "sus")]
    public sealed class SusImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            var asset = new TextAsset(text);
            ctx.AddObjectToAsset("text", asset);
            ctx.SetMainObject(asset);

            try
            {
                var warnings = new List<string>();
                var chart = SusConverter.ToChart(text, warnings);
                foreach (var warning in warnings) Debug.LogWarning($"{ctx.assetPath}: {warning}");
                Debug.Log($"{ctx.assetPath}: ノーツ {chart.Notes.Count} 個として読み込めます。");
            }
            catch (ChartFormatException e)
            {
                Debug.LogError($"{ctx.assetPath}: 譜面として読めません: {e.Message}");
            }
        }
    }
}
