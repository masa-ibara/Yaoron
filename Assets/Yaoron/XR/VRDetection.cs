using Yaoron.Core;
using UnityEngine;

namespace Yaoron.XR
{
    /// <summary>
    /// 起動時にどのリグを生かすかを決める (設計書 §10)。
    /// Windows アプリは 1 バイナリでデスクトップと PC VR を兼ねるため、
    /// HMD が接続されていなければ VR ローダーを止めてデスクトップとして立ち上げる。
    /// </summary>
    public class VRDetection : MonoBehaviour
    {
        [SerializeField] GameObject _desktopRig;
        [SerializeField] GameObject _mobileRig;
        [SerializeField] GameObject _xrRig;

        void Awake()
        {
            PlatformProfile.EnsureInitialized();

            bool vr = PlatformProfile.IsVR;
            bool mobile = PlatformProfile.IsMobile;

            // 有効化は 1 つだけ。無効側のカメラが残ると音のリスナーが二重になる。
            if (_xrRig != null) _xrRig.SetActive(vr);
            if (_mobileRig != null) _mobileRig.SetActive(!vr && mobile);
            if (_desktopRig != null) _desktopRig.SetActive(!vr && !mobile);

            YaLog.Info($"有効なリグ: {(vr ? "XR" : mobile ? "Mobile" : "Desktop")}");
        }
    }
}
