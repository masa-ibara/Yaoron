using UnityEngine;
using UnityEngine.XR;

namespace Yaoron.Core
{
    public enum RigKind { Desktop, Mobile, VR }

    /// <summary>音声の空間化方式。Web はブラウザ直再生のため距離減衰が効かない (設計書 ADR-4)。</summary>
    public enum VoiceSpatialization { Spatial3D, Flat2D }

    /// <summary>
    /// 実行中プラットフォームの一次判定。入力・リグ・音声・品質の分岐はすべてここを見る。
    /// VR 判定は HMD の実在をランタイムで確認するので、Windows アプリは 1 バイナリで
    /// デスクトップと PC VR の両方を兼ねる (設計書 §10)。
    /// </summary>
    public static class PlatformProfile
    {
        static bool _initialized;
        static RigKind _rig;

        public static RigKind Rig
        {
            get { EnsureInitialized(); return _rig; }
        }

        public static bool IsVR => Rig == RigKind.VR;
        public static bool IsMobile => Rig == RigKind.Mobile;

        public static bool IsWeb
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Web は FMOD が無く距離減衰が効かないため 2D 固定。</summary>
        public static VoiceSpatialization Voice => IsWeb ? VoiceSpatialization.Flat2D : VoiceSpatialization.Spatial3D;

        /// <summary>WebGL はスレッドが使えないため VRM ロードを NoThread に切り替える必要がある。</summary>
        public static bool SupportsThreads => !IsWeb;

        public static int TargetFrameRate
        {
            get
            {
                if (IsVR) return -1;              // XR 側 (Quest 72 Hz など) に任せる
                return IsMobile ? 60 : 60;
            }
        }

        /// <summary>同時にフル品質で描画するリモートアバター数の上限 (設計書 §12)。</summary>
        public static int MaxFullQualityAvatars
        {
            get
            {
                if (IsVR) return 12;
                if (IsMobile) return 10;
                if (IsWeb) return 15;
                return 30;
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _rig = DetectRig();
            YaLog.Info($"PlatformProfile: rig={_rig} web={IsWeb} voice={Voice}");
        }

        /// <summary>VR 判定は起動時 1 回。Quest は常に VR、PC は HMD が接続されていれば VR。</summary>
        static RigKind DetectRig()
        {
#if UNITY_ANDROID && YA_QUEST_BUILD
            return RigKind.VR;
#elif UNITY_IOS || UNITY_ANDROID
            return RigKind.Mobile;
#elif UNITY_WEBGL
            return RigKind.Desktop;   // WebXR は対象外 (設計書 §1 非スコープ)
#else
            return HasConnectedHmd() ? RigKind.VR : RigKind.Desktop;
#endif
        }

        /// <summary>
        /// XR Plug-in Management が OpenXR ローダーを起動済みで、かつ HMD ノードが有効なときだけ VR。
        /// XRSettings / InputDevices は built-in の XR モジュールなので XR パッケージ未導入でもコンパイルできる。
        /// </summary>
        static bool HasConnectedHmd()
        {
            if (!XRSettings.enabled || !XRSettings.isDeviceActive) return false;
            if (string.IsNullOrEmpty(XRSettings.loadedDeviceName)) return false;
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return hmd.isValid;
        }

        /// <summary>HMD の抜き差しなど、外部から明示的に再判定させたい場合。</summary>
        public static void ForceRig(RigKind rig)
        {
            _initialized = true;
            _rig = rig;
        }
    }
}
