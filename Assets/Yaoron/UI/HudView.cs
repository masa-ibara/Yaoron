using Yaoron.Core;
using Yaoron.Net;
using Yaoron.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace Yaoron.UI
{
    /// <summary>
    /// 入室後の常設 HUD: ルーム名と人数、接続状態、ミュート切替、発話インジケータ。
    /// 人数は RoomCapacityGuard が数えた値をそのまま出す (設計書 §9)。
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] SessionController _session;
        [SerializeField] RoomCapacityGuard _capacity;
        [SerializeField] Text _roomLabel;
        [SerializeField] Text _stateLabel;
        [SerializeField] Button _muteButton;
        [SerializeField] Text _muteLabel;
        [SerializeField] Image _speakingIndicator;
        [SerializeField] Button _leaveButton;
        [SerializeField] float _refreshInterval = 0.5f;

        IVoiceService _voice;
        float _nextRefresh;

        void Awake()
        {
            if (_session == null) _session = FindFirstObjectByType<SessionController>();
            if (_capacity == null) _capacity = FindFirstObjectByType<RoomCapacityGuard>();
            if (_muteButton != null) _muteButton.onClick.AddListener(ToggleMute);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(Leave);
        }

        void OnDestroy()
        {
            if (_muteButton != null) _muteButton.onClick.RemoveListener(ToggleMute);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(Leave);
        }

        void Update()
        {
            _voice ??= YaServices.Get<IVoiceService>();

            if (_speakingIndicator != null && _voice != null)
            {
                float level = Mathf.Clamp01(_voice.LocalLevel * 6f);
                var color = _speakingIndicator.color;
                color.a = _voice.IsMuted ? 0.15f : Mathf.Lerp(0.2f, 1f, level);
                _speakingIndicator.color = color;
            }

            // 人数の走査は毎フレームやる必要がない。
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + _refreshInterval;
            Refresh();
        }

        void Refresh()
        {
            if (_roomLabel != null && _session != null)
            {
                var occupancy = _capacity != null ? _capacity.OccupancyLabel : "-";
                _roomLabel.text = $"{_session.RoomName}   {occupancy}";
            }

            if (_stateLabel != null && _session != null)
                _stateLabel.text = Describe(_session.State);

            if (_muteLabel != null && _voice != null)
                _muteLabel.text = _voice.IsMuted ? "ミュート中" : (_voice.PushToTalk ? "PTT" : "送信中");
        }

        static string Describe(SessionState state) => state switch
        {
            SessionState.Idle => "未接続",
            SessionState.Connecting => "接続中…",
            SessionState.Connected => "接続済み",
            SessionState.Reconnecting => "再接続中…",
            SessionState.Failed => "接続失敗",
            _ => string.Empty,
        };

        void ToggleMute()
        {
            _voice ??= YaServices.Get<IVoiceService>();
            if (_voice == null) return;
            _voice.IsMuted = !_voice.IsMuted;
            Refresh();
        }

        void Leave()
        {
            if (_session != null) _session.Leave();
        }
    }
}
