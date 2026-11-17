using System;
using System.IO;
using LunarTrails.Core.Records;
using UnityEngine;

namespace LunarTrails.Runtime.Records
{
    /// <summary>
    /// 自己ベストをファイル（JSON）に保存・読み込みする。
    /// 保存先は Application.persistentDataPath/records.json（設定の settings.json と同じフォルダ）。
    /// </summary>
    public static class RecordStore
    {
        public static string FilePath => Path.Combine(Application.persistentDataPath, "records.json");

        public static RecordBook Load()
        {
            var book = new RecordBook();
            try
            {
                if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), book);
            }
            catch (Exception e)
            {
                // 壊れたファイルでも起動はできるようにする。上書きで消さないよう、壊れたファイルは別名で残す
                Debug.LogWarning($"記録ファイルを読めなかったので空の記録で始めます: {e.Message}");
                TryBackup();
                book = new RecordBook();
            }
            book.Sanitize();
            return book;
        }

        public static void Save(RecordBook book)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(book, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"記録を保存できませんでした: {e.Message}");
            }
        }

        private static void TryBackup()
        {
            try
            {
                File.Copy(FilePath, FilePath + ".broken", overwrite: true);
            }
            catch (Exception)
            {
                // 残せなくても続ける
            }
        }
    }
}
