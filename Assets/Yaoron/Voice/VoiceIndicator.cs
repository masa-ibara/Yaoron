using Yaoron.Avatar;
using Yaoron.Core;
using UnityEngine;

namespace Yaoron.Voice
{
    /// <summary>
    /// アバターの頭上に出る発話インジケータ。発話量はネットワークに流さず、
    /// 受信した音声の音量から各クライアントが自分で作る (設計書 §7)。
    /// </summary>
    public class VoiceIndicator : MonoBehaviour
    {
        [SerializeField] YaAvatar _avatar;
        [SerializeField] GameObject _icon;
        [SerializeField] float _showThreshold = 0.02f;
        [SerializeField] float _hideDelay = 0.35f;

        NormcoreVoiceService _voice;
        float _lastSpeakTime = -10f;

        void Awake()
        {
            if (_avatar == null) _avatar = GetComponentInParent<YaAvatar>();
            if (_icon != null) _icon.SetActive(false);
        }

        void Update()
        {
            if (_avatar == null || _icon == null) return;
            if (_voice == null)
            {
                _voice = YaServices.Get<NormcoreVoiceService>();
                if (_voice == null) return;
            }

            if (_voice.VolumeOf(_avatar) > _showThreshold) _lastSpeakTime = Time.unscaledTime;

            // 短い無音でちらつかせないよう、少し引っ張ってから消す。
            bool visible = Time.unscaledTime - _lastSpeakTime < _hideDelay;
            if (_icon.activeSelf != visible) _icon.SetActive(visible);
        }
    }
}
