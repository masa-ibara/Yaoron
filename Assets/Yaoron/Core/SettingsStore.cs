using UnityEngine;

namespace Yaoron.Core
{
    /// <summary>
    /// 表示名・アバター選択・音声設定の永続化 (PlayerPrefs)。
    /// 切断復帰時に再 Instantiate するだけで元の見た目に戻せるよう、ここが唯一の真実になる。
    /// </summary>
    public static class SettingsStore
    {
        const string KeyDisplayName = "yaoron.displayName";
        const string KeyAvatarId    = "yaoron.avatarId";
        const string KeyMuted       = "yaoron.voice.muted";
        const string KeyPtt         = "yaoron.voice.ptt";
        const string KeyVolume      = "yaoron.voice.volume";
        const string KeyLastRoom    = "yaoron.room.last";

        public static string DisplayName
        {
            get => PlayerPrefs.GetString(KeyDisplayName, string.Empty);
            set { PlayerPrefs.SetString(KeyDisplayName, Sanitize(value)); PlayerPrefs.Save(); }
        }

        public static string AvatarId
        {
            get => PlayerPrefs.GetString(KeyAvatarId, string.Empty);
            set { PlayerPrefs.SetString(KeyAvatarId, value ?? string.Empty); PlayerPrefs.Save(); }
        }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(KeyMuted, 0) == 1;
            set { PlayerPrefs.SetInt(KeyMuted, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool PushToTalk
        {
            get => PlayerPrefs.GetInt(KeyPtt, 0) == 1;
            set { PlayerPrefs.SetInt(KeyPtt, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static string LastRoom
        {
            get => PlayerPrefs.GetString(KeyLastRoom, string.Empty);
            set { PlayerPrefs.SetString(KeyLastRoom, value ?? string.Empty); PlayerPrefs.Save(); }
        }

        public static bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayName);

        /// <summary>ネームプレートに出る文字列なので、改行・制御文字・過長を落としておく。</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var buffer = new System.Text.StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsControl(c)) continue;
                buffer.Append(c);
                if (buffer.Length >= 16) break;
            }
            return buffer.ToString().Trim();
        }
    }
}
