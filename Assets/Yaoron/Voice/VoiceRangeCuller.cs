using System.Collections;
using Yaoron.Avatar;
using Yaoron.Core;
using UnityEngine;
#if YA_NORMCORE
using Normal.Realtime;
#endif

namespace Yaoron.Voice
{
    /// <summary>
    /// 「聞こえる範囲」をローカルで決める (設計書 §7, ADR-4)。
    /// Web は音声がブラウザ直再生で距離減衰が効かないため、これが唯一の距離制御になる。
    /// アプリ版でも 20 m 超を完全ミュートする用途で併用し、30 人分の声が重なる事故を防ぐ。
    /// 境界での断続を避けるため、解除は radius - hysteresis、ミュートは radius + hysteresis。
    /// </summary>
    public class VoiceRangeCuller : MonoBehaviour
    {
        [SerializeField] float _radius = 20f;
        [SerializeField] float _hysteresis = 2f;
        [SerializeField] float _interval = 0.5f;

        public float Radius => _radius;

        void Awake() => YaServices.Register(this);

        void OnDestroy()
        {
            if (YaServices.Get<VoiceRangeCuller>() == this) YaServices.Unregister<VoiceRangeCuller>();
        }

        void OnEnable() => StartCoroutine(CullLoop());

        public void SetRadius(float meters)
        {
            _radius = Mathf.Max(1f, meters);
        }

        IEnumerator CullLoop()
        {
            var wait = new WaitForSeconds(_interval);
            while (true)
            {
                yield return wait;
                Cull();
            }
        }

        void Cull()
        {
            var manager = YaAvatarManager.Instance;
            var local = manager != null ? manager.LocalAvatar : null;
            if (local == null) return;

            var origin = local.Root.position;
            var avatars = YaAvatarManager.All;

            for (int i = 0; i < avatars.Count; i++)
            {
                var avatar = avatars[i];
                if (avatar == null || avatar.IsLocal) continue;

                float distance = Vector3.Distance(origin, avatar.Root.position);
                bool muted = IsMuted(avatar);

                if (!muted && distance > _radius + _hysteresis) SetMuted(avatar, true);
                else if (muted && distance < _radius - _hysteresis) SetMuted(avatar, false);
            }
        }

        static bool IsMuted(YaAvatar avatar)
        {
#if YA_NORMCORE
            var voice = avatar.GetComponentInChildren<RealtimeAvatarVoice>(true);
            if (voice != null) return voice.mute;
#endif
            var source = avatar.GetComponentInChildren<AudioSource>(true);
            return source != null && source.mute;
        }

        static void SetMuted(YaAvatar avatar, bool mute)
        {
#if YA_NORMCORE
            // リモートのインスタンスに対する mute はローカル再生だけに効く (相手の送信は止めない)。
            var voice = avatar.GetComponentInChildren<RealtimeAvatarVoice>(true);
            if (voice != null) voice.mute = mute;
#endif
            var source = avatar.GetComponentInChildren<AudioSource>(true);
            if (source != null) source.mute = mute;
        }
    }
}
