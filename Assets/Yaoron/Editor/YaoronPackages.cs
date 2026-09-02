using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 依存パッケージの導入。バージョンを固定せず Package Manager に解決させるので、
    /// Unity のバージョンが変わっても壊れない。Normcore のスコープ付きレジストリは
    /// Packages/manifest.json に登録済み。
    /// </summary>
    public static class YaoronPackages
    {
        public const string Normcore = "com.normalvr.normcore";
        public const string InputSystem = "com.unity.inputsystem";
        public const string XrManagement = "com.unity.xr.management";
        public const string OpenXr = "com.unity.xr.openxr";
        public const string UniTaskGit = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";

        /// <summary>
        /// UniVRM (VRM 1.0) は UniGLTF と VRM10 の 2 パッケージ。依存があるので同時に入れる。
        /// 0.128 より前は VRMShaders が別パッケージだったので、古いタグを使う場合は追加が必要。
        /// </summary>
        public static string[] UniVrmGit(string tag) => new[]
        {
            $"https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#{tag}",
            $"https://github.com/vrm-c/UniVRM.git?path=/Assets/VRM10#{tag}",
        };

        static AddAndRemoveRequest _request;
        static Action<bool> _onDone;

        public static bool Busy => _request != null && !_request.IsCompleted;

        /// <summary>複数まとめて 1 回の解決で入れる (依存が絡むパッケージは分けると失敗する)。</summary>
        public static void Install(IEnumerable<string> identifiers, Action<bool> onDone = null)
        {
            if (Busy)
            {
                Debug.LogWarning("[Yaoron] パッケージ導入が進行中です。");
                return;
            }

            var list = new List<string>(identifiers);
            if (list.Count == 0) return;

            Debug.Log("[Yaoron] パッケージを導入します: " + string.Join(", ", list));
            _onDone = onDone;
            _request = Client.AddAndRemove(list.ToArray(), null);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= Poll;

            bool ok = _request.Status == StatusCode.Success;
            if (ok) Debug.Log("[Yaoron] パッケージの導入が完了しました。");
            else Debug.LogError($"[Yaoron] パッケージの導入に失敗しました: {_request.Error?.message}");

            _request = null;
            var callback = _onDone;
            _onDone = null;
            callback?.Invoke(ok);

            // 導入直後に定義シンボルを合わせる (再コンパイル後にもう一度走る)。
            YaoronDefines.Sync();
        }
    }
}
