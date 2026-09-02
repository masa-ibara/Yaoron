#if YA_NORMCORE
using Yaoron.Core;
using Normal.Realtime;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// YaAvatar (ネットワーク非依存) と Normcore の Datastore を繋ぐ唯一の場所。
    /// オーナー側はローカル状態をモデルへ、それ以外はモデルの変更をローカル状態へ流す。
    /// unreliable な moveDir だけは非VR で 30 Hz に間引く (設計書 §6「送信レートと補間」)。
    /// </summary>
    [RequireComponent(typeof(YaAvatar))]
    public class AvatarSync : RealtimeComponent<AvatarStateModel>
    {
        [SerializeField] float _nonVrWriteHz = 30f;

        YaAvatar _avatar;
        float _nextWriteTime;
        Vector2 _lastSentMoveDir;

        void Awake()
        {
            _avatar = GetComponent<YaAvatar>();
        }

        protected override void OnRealtimeModelReplaced(AvatarStateModel previousModel, AvatarStateModel currentModel)
        {
            if (previousModel != null)
            {
                previousModel.avatarIdDidChange     -= OnAvatarIdChanged;
                previousModel.displayNameDidChange  -= OnDisplayNameChanged;
                previousModel.isVRDidChange         -= OnIsVRChanged;
                previousModel.locomotionDidChange   -= OnLocomotionChanged;
                previousModel.expressionDidChange   -= OnExpressionChanged;
            }

            if (currentModel == null) return;

            if (currentModel.isFreshModel)
            {
                // 生成直後のオーナーだけが初期値を書き込む。
                currentModel.avatarId    = SettingsStore.AvatarId;
                currentModel.displayName = SettingsStore.DisplayName;
                currentModel.isVR        = PlatformProfile.IsVR;
                currentModel.locomotion  = (byte)Locomotion.Idle;
                currentModel.expression  = (byte)ExpressionPreset.Neutral;
            }

            currentModel.avatarIdDidChange    += OnAvatarIdChanged;
            currentModel.displayNameDidChange += OnDisplayNameChanged;
            currentModel.isVRDidChange        += OnIsVRChanged;
            currentModel.locomotionDidChange  += OnLocomotionChanged;
            currentModel.expressionDidChange  += OnExpressionChanged;

            // 既存プレイヤーの分は接続時にスナップショットで届くので、初回は自分で流し込む。
            _avatar.State.ApplyRemote(
                currentModel.avatarId,
                currentModel.displayName,
                currentModel.isVR,
                currentModel.locomotion,
                currentModel.moveDir,
                currentModel.expression);

            _avatar.Initialize(isOwnedLocallyInHierarchy, ownerIDInHierarchy);

            if (isOwnedLocallyInHierarchy) HookLocalState();
        }

        void HookLocalState()
        {
            var state = _avatar.State;
            state.AvatarIdChanged    += v => { if (model != null) model.avatarId = v; };
            state.DisplayNameChanged += v => { if (model != null) model.displayName = v; };
            state.IsVRChanged        += v => { if (model != null) model.isVR = v; };
            state.LocomotionChanged  += v => { if (model != null) model.locomotion = (byte)v; };
            state.ExpressionChanged  += v => { if (model != null) model.expression = (byte)v; };
        }

        void LateUpdate()
        {
            if (model == null || !isOwnedLocallyInHierarchy) return;

            // VR は毎フレーム、それ以外は 30 Hz。差が無いときは書かない (差分が出ないので送信もされない)。
            if (!PlatformProfile.IsVR)
            {
                if (Time.unscaledTime < _nextWriteTime) return;
                _nextWriteTime = Time.unscaledTime + 1f / Mathf.Max(1f, _nonVrWriteHz);
            }

            var moveDir = _avatar.State.MoveDir;
            if ((moveDir - _lastSentMoveDir).sqrMagnitude < 0.0004f) return;
            _lastSentMoveDir = moveDir;
            model.moveDir = moveDir;
        }

        void OnAvatarIdChanged(AvatarStateModel m, string value)    => _avatar.State.AvatarId = value;
        void OnDisplayNameChanged(AvatarStateModel m, string value) => _avatar.State.DisplayName = value;
        void OnIsVRChanged(AvatarStateModel m, bool value)          => _avatar.State.IsVR = value;
        void OnLocomotionChanged(AvatarStateModel m, byte value)    => _avatar.State.Locomotion = (Locomotion)value;
        void OnExpressionChanged(AvatarStateModel m, byte value)    => _avatar.State.Expression = (ExpressionPreset)value;

        void Update()
        {
            // moveDir は unreliable なので didChange を持たせていない。リモートは毎フレーム読む。
            if (model == null || isOwnedLocallyInHierarchy) return;
            _avatar.State.MoveDir = model.moveDir;
        }
    }
}
#endif
