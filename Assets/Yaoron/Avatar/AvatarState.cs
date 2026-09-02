using System;
using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>移動状態。同期は byte 1 個で足りる (設計書 §6)。</summary>
    public enum Locomotion : byte { Idle = 0, Walk = 1, Run = 2, Jump = 3, Sit = 4 }

    /// <summary>VRM 1.0 のプリセット表情に対応する。</summary>
    public enum ExpressionPreset : byte { Neutral = 0, Happy = 1, Angry = 2, Sad = 3, Relaxed = 4, Surprised = 5 }

    /// <summary>
    /// アバター 1 体分の同期対象状態を、ネットワーク実装から切り離して保持する器。
    /// Normcore がある場合は AvatarSync が AvatarStateModel と双方向に橋渡しし、
    /// 無い場合 (オフライン単独) でもローカル値としてそのまま機能する。
    /// </summary>
    public class AvatarState
    {
        string _avatarId = string.Empty;
        string _displayName = string.Empty;
        bool _isVR;
        Locomotion _locomotion = Locomotion.Idle;
        Vector2 _moveDir;
        ExpressionPreset _expression = ExpressionPreset.Neutral;

        public event Action<string> AvatarIdChanged;
        public event Action<string> DisplayNameChanged;
        public event Action<bool> IsVRChanged;
        public event Action<Locomotion> LocomotionChanged;
        public event Action<ExpressionPreset> ExpressionChanged;

        /// <summary>プリセット VRM の ID。本体は流さず ID だけ同期する (設計書 ADR-3)。</summary>
        public string AvatarId
        {
            get => _avatarId;
            set
            {
                var v = value ?? string.Empty;
                if (_avatarId == v) return;
                _avatarId = v;
                AvatarIdChanged?.Invoke(v);
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                var v = value ?? string.Empty;
                if (_displayName == v) return;
                _displayName = v;
                DisplayNameChanged?.Invoke(v);
            }
        }

        /// <summary>受信側が 3 点 IK と非VR ポーズのどちらで復元するかの判断に使う。</summary>
        public bool IsVR
        {
            get => _isVR;
            set { if (_isVR == value) return; _isVR = value; IsVRChanged?.Invoke(value); }
        }

        public Locomotion Locomotion
        {
            get => _locomotion;
            set { if (_locomotion == value) return; _locomotion = value; LocomotionChanged?.Invoke(value); }
        }

        /// <summary>ブレンドツリー用の入力方向 (unreliable / 補間前提)。</summary>
        public Vector2 MoveDir
        {
            get => _moveDir;
            set => _moveDir = value;
        }

        public ExpressionPreset Expression
        {
            get => _expression;
            set { if (_expression == value) return; _expression = value; ExpressionChanged?.Invoke(value); }
        }

        /// <summary>ネットワーク側から来た値を、変更イベント付きでまとめて流し込む。</summary>
        public void ApplyRemote(string avatarId, string displayName, bool isVR, byte locomotion, Vector2 moveDir, byte expression)
        {
            AvatarId = avatarId;
            DisplayName = displayName;
            IsVR = isVR;
            Locomotion = (Locomotion)locomotion;
            MoveDir = moveDir;
            Expression = (ExpressionPreset)expression;
        }
    }
}
