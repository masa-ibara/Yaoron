using Yaoron.Avatar;
using Yaoron.Core;
using Yaoron.Net;
using UnityEngine;
using UnityEngine.UI;

namespace Yaoron.UI
{
    /// <summary>
    /// 入室前の画面: 表示名の入力とプリセットアバターの選択、そして入室 (設計書 §9)。
    /// ブラウザで自動再生とマイク要求が弾かれないよう、Realtime.Connect はこのボタン起点で呼ぶ。
    /// </summary>
    public class JoinPanel : MonoBehaviour
    {
        [SerializeField] GameObject _panel;
        [SerializeField] InputField _nameField;
        [SerializeField] Dropdown _avatarDropdown;
        [SerializeField] Button _joinButton;
        [SerializeField] Text _status;

        [SerializeField] SessionController _session;
        [SerializeField] AvatarLoader _loader;
        [SerializeField] PermissionFlow _permissions;

        void Awake()
        {
            if (_session == null) _session = FindFirstObjectByType<SessionController>();
            if (_loader == null) _loader = FindFirstObjectByType<AvatarLoader>();
            if (_permissions == null) _permissions = FindFirstObjectByType<PermissionFlow>();

            if (_nameField != null) _nameField.text = SettingsStore.DisplayName;
            PopulateAvatars();

            if (_joinButton != null) _joinButton.onClick.AddListener(OnJoinClicked);
            if (_session != null) _session.StateChanged += OnStateChanged;
            Show(true);
        }

        void OnDestroy()
        {
            if (_joinButton != null) _joinButton.onClick.RemoveListener(OnJoinClicked);
            if (_session != null) _session.StateChanged -= OnStateChanged;
        }

        void PopulateAvatars()
        {
            if (_avatarDropdown == null) return;
            _avatarDropdown.ClearOptions();

            var catalog = _loader != null ? _loader.Catalog : null;
            if (catalog == null || catalog.Entries.Count == 0)
            {
                _avatarDropdown.options.Add(new Dropdown.OptionData("(カタログ未設定)"));
                _avatarDropdown.interactable = false;
                _avatarDropdown.RefreshShownValue();
                return;
            }

            int selected = 0;
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                _avatarDropdown.options.Add(new Dropdown.OptionData(
                    string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName));
                if (entry.id == SettingsStore.AvatarId) selected = i;
            }
            _avatarDropdown.value = selected;
            _avatarDropdown.RefreshShownValue();
        }

        async void OnJoinClicked()
        {
            if (_joinButton != null) _joinButton.interactable = false;
            SetStatus("マイクの使用を確認しています…");

            SettingsStore.DisplayName = _nameField != null ? _nameField.text : SettingsStore.DisplayName;
            SettingsStore.AvatarId = SelectedAvatarId();

            if (_permissions != null) await _permissions.EnsureMicrophoneAsync();

            SetStatus("接続しています…");
            if (_session != null) _session.Join();
        }

        string SelectedAvatarId()
        {
            var catalog = _loader != null ? _loader.Catalog : null;
            if (catalog == null || catalog.Entries.Count == 0 || _avatarDropdown == null)
                return SettingsStore.AvatarId;
            int index = Mathf.Clamp(_avatarDropdown.value, 0, catalog.Entries.Count - 1);
            return catalog.Entries[index].id;
        }

        void OnStateChanged(SessionState state)
        {
            switch (state)
            {
                case SessionState.Connected:
                    Show(false);
                    break;
                case SessionState.Failed:
                    SetStatus("接続できませんでした。時間をおいて再試行してください。");
                    Show(true);
                    if (_joinButton != null) _joinButton.interactable = true;
                    break;
                case SessionState.Reconnecting:
                    SetStatus("再接続しています…");
                    break;
                case SessionState.Idle:
                    Show(true);
                    if (_joinButton != null) _joinButton.interactable = true;
                    break;
            }
        }

        void Show(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        void SetStatus(string message)
        {
            if (_status != null) _status.text = message;
        }
    }
}
