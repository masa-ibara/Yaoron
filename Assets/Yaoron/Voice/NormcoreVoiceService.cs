using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yaoron.Avatar;
using Yaoron.Core;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if YA_NORMCORE
using Normal.Realtime;
#endif

namespace Yaoron.Voice
{
    /// <summary>
    /// Normcore の RealtimeAvatarVoice を包む音声サービス (設計書 §7)。
    /// 送受信そのものは SDK が持つので、ここが持つのは
    /// ミュート / PTT / 発話量の観測 / 口パクと発話インジケータへの配線だけ。
    /// Normcore 未導入でもミュート状態や権限フローは動く (音は出ない)。
    /// </summary>
    public class NormcoreVoiceService : MonoBehaviour, IVoiceService
    {
        [Tooltip("この発話量を超えたら「話している」とみなす")]
        [SerializeField] float _speakingThreshold = 0.02f;
        [Tooltip("口パクの追従の速さ")]
        [SerializeField] float _mouthSmoothing = 12f;
        [SerializeField] float _mouthGain = 3f;

        readonly Dictionary<int, bool> _speaking = new Dictionary<int, bool>();

        bool _muted;
        bool _pushToTalk;
        bool _pttHeld;
        float _listenRadius = 20f;

        public event Action<int, bool> RemoteSpeakingChanged;

        public bool IsMuted
        {
            get => _muted;
            set { _muted = value; SettingsStore.Muted = value; ApplyMute(); }
        }

        public bool PushToTalk
        {
            get => _pushToTalk;
            set { _pushToTalk = value; SettingsStore.PushToTalk = value; ApplyMute(); }
        }

        /// <summary>PTT の押下状態。入力層から毎フレーム流し込む。</summary>
        public bool PushToTalkHeld
        {
            get => _pttHeld;
            set { if (_pttHeld == value) return; _pttHeld = value; ApplyMute(); }
        }

        public float LocalLevel { get; private set; }
        public bool HasMicrophonePermission { get; private set; }
        public float ListenRadius => _listenRadius;

        void Awake()
        {
            YaServices.Register<IVoiceService>(this);
            YaServices.Register(this);
            _muted = SettingsStore.Muted;
            _pushToTalk = SettingsStore.PushToTalk;
        }

        void OnDestroy()
        {
            if (YaAvatarManager.Instance != null)
                YaAvatarManager.Instance.AvatarRemoved -= Forget;
            YaServices.Unregister<IVoiceService>();
            YaServices.Unregister<NormcoreVoiceService>();
        }

        void Start()
        {
            if (YaAvatarManager.Instance != null)
                YaAvatarManager.Instance.AvatarRemoved += Forget;
        }

        /// <summary>退出したアバターの分は口パク・発話状態の記録から落とす。</summary>
        void Forget(YaAvatar avatar)
        {
            _mouth.Remove(avatar);
            if (_speaking.Remove(avatar.OwnerId)) RemoteSpeakingChanged?.Invoke(avatar.OwnerId, false);
        }

        void Update()
        {
            ApplyMute();
            UpdateLevels();
        }

        // ------------------------------------------------------------ ミュート

        /// <summary>送信するかどうかの最終判断。PTT 有効時は押している間だけ送る。</summary>
        bool ShouldTransmit()
        {
            if (_muted) return false;
            if (_pushToTalk && !_pttHeld) return false;
            return true;
        }

        void ApplyMute()
        {
#if YA_NORMCORE
            var voice = LocalVoice();
            if (voice == null) return;
            bool mute = !ShouldTransmit();
            if (voice.mute != mute) voice.mute = mute;
#endif
        }

#if YA_NORMCORE
        RealtimeAvatarVoice LocalVoice()
        {
            var local = YaAvatarManager.Instance != null ? YaAvatarManager.Instance.LocalAvatar : null;
            return local != null ? local.GetComponentInChildren<RealtimeAvatarVoice>(true) : null;
        }
#endif

        // ------------------------------------------------------------ 発話量

        void UpdateLevels()
        {
            var avatars = YaAvatarManager.All;
            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (avatar == null) continue;

                float level = VolumeOf(avatar);
                if (avatar.IsLocal) LocalLevel = level;

                // 口パクは同期せず、各クライアントが受信した音量から作る (設計書 §7)。
                var solver = avatar.PoseSolver;
                if (solver != null)
                {
                    float target = Mathf.Clamp01(level * _mouthGain);
                    float smoothed = Mathf.Lerp(PreviousMouth(avatar), target, Time.deltaTime * _mouthSmoothing);
                    solver.SetMouthOpen(smoothed);
                    _mouth[avatar] = smoothed;
                }

                bool speaking = level > _speakingThreshold;
                if (!_speaking.TryGetValue(avatar.OwnerId, out var was) || was != speaking)
                {
                    _speaking[avatar.OwnerId] = speaking;
                    RemoteSpeakingChanged?.Invoke(avatar.OwnerId, speaking);
                }
            }
        }

        readonly Dictionary<YaAvatar, float> _mouth = new Dictionary<YaAvatar, float>();
        float PreviousMouth(YaAvatar avatar) => _mouth.TryGetValue(avatar, out var v) ? v : 0f;

        public float VolumeOf(YaAvatar avatar)
        {
#if YA_NORMCORE
            var voice = avatar.GetComponentInChildren<RealtimeAvatarVoice>(true);
            return voice != null ? voice.voiceVolume : 0f;
#else
            return 0f;
#endif
        }

        public bool IsSpeaking(int clientId) => _speaking.TryGetValue(clientId, out var v) && v;

        // ------------------------------------------------------------ 権限

        /// <summary>入室前に呼ぶ。プラットフォームごとの許可要求を吸収する (設計書 §7)。</summary>
        public Task<bool> RequestMicrophoneAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            YaAwait.Run(RequestMicrophoneRoutine(tcs));
            return tcs.Task;
        }

        IEnumerator RequestMicrophoneRoutine(TaskCompletionSource<bool> tcs)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Quest も Android なのでここを通る。入室前に取得しておく。
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone)
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
            HasMicrophonePermission = Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            HasMicrophonePermission = Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            if (!HasMicrophonePermission) YaLog.Warn("マイクの使用が許可されませんでした。");
            tcs.TrySetResult(HasMicrophonePermission);
        }

        public void SetListenRadius(float meters)
        {
            _listenRadius = Mathf.Max(1f, meters);
            var culler = YaServices.Get<VoiceRangeCuller>();
            if (culler != null) culler.SetRadius(_listenRadius);
        }
    }
}
