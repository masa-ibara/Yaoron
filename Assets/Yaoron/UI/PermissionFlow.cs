using System.Threading.Tasks;
using Yaoron.Core;
using Yaoron.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace Yaoron.UI
{
    /// <summary>
    /// 入室前のマイク権限フロー (設計書 §7, §9)。
    /// ブラウザは「ユーザー操作の直後」でないと要求が通らないので、
    /// 入室ボタンのハンドラからそのまま呼ぶこと。
    /// </summary>
    public class PermissionFlow : MonoBehaviour
    {
        [SerializeField] GameObject _deniedPanel;
        [SerializeField] Text _deniedMessage;

        void Awake()
        {
            if (_deniedPanel != null) _deniedPanel.SetActive(false);
            YaServices.Register(this);
        }

        void OnDestroy()
        {
            if (YaServices.Get<PermissionFlow>() == this) YaServices.Unregister<PermissionFlow>();
        }

        /// <summary>権限が取れなくても入室自体は許す (聞くだけの参加は成立する)。</summary>
        public async Task<bool> EnsureMicrophoneAsync()
        {
            var voice = YaServices.Get<IVoiceService>();
            if (voice == null) return false;

            bool granted = await voice.RequestMicrophoneAsync();
            if (!granted)
            {
                ShowDenied("マイクを使えないため、聞き専で入室します。ブラウザ / OS の設定から許可できます。");
                voice.IsMuted = true;
            }
            return granted;
        }

        void ShowDenied(string message)
        {
            if (_deniedMessage != null) _deniedMessage.text = message;
            if (_deniedPanel != null) _deniedPanel.SetActive(true);
            YaLog.Warn(message);
        }
    }
}
