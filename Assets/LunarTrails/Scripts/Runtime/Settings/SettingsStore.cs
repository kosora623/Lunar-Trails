using System;
using System.IO;
using LunarTrails.Core.Settings;
using UnityEngine;

namespace LunarTrails.Runtime.Settings
{
    /// <summary>
    /// 設定をファイル（JSON）に保存・読み込みする。
    /// 保存先は Application.persistentDataPath/settings.json（OS ごとのユーザー用フォルダ）。
    /// </summary>
    public static class SettingsStore
    {
        // 以前の試作でオフセットだけを PlayerPrefs に保存していたときのキー。初回だけ引き継ぐ
        private const string LegacyOffsetPrefsKey = "LunarTrails.PlayerOffsetMs";

        public static string FilePath => Path.Combine(Application.persistentDataPath, "settings.json");

        public static GameSettings Load()
        {
            var settings = new GameSettings();
            try
            {
                if (File.Exists(FilePath))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), settings);
                }
                else if (PlayerPrefs.HasKey(LegacyOffsetPrefsKey))
                {
                    settings.offsetMs = PlayerPrefs.GetInt(LegacyOffsetPrefsKey);
                }
            }
            catch (Exception e)
            {
                // 壊れたファイルでもゲームは起動できるようにする（初期値で続ける）
                Debug.LogWarning($"設定ファイルを読めなかったので初期値を使います: {e.Message}");
                settings = new GameSettings();
            }
            settings.Sanitize();
            return settings;
        }

        public static void Save(GameSettings settings)
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(settings, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"設定を保存できませんでした: {e.Message}");
            }
        }
    }
}
