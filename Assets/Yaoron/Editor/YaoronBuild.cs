using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 設計書 §11 の 6 プロファイル分のビルド入口。
    /// Unity 6 の Build Profiles が使える環境ではそちらでも構わないが、
    /// CI から叩けるようにコード側にも置いておく (-executeMethod で呼べる)。
    /// </summary>
    public static class YaoronBuild
    {
        const string OutputRoot = "Builds";

        [MenuItem("Yaoron/ビルド/Windows", priority = 40)]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows/Yaoron.exe");

        [MenuItem("Yaoron/ビルド/macOS", priority = 41)]
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "macOS/Yaoron.app");

        [MenuItem("Yaoron/ビルド/Android", priority = 42)]
        public static void Android() => Build(BuildTarget.Android, "Android/Yaoron.apk");

        /// <summary>Quest は Android ビルドの派生。VR 判定を固定するため定義シンボルを足す。</summary>
        [MenuItem("Yaoron/ビルド/Quest", priority = 43)]
        public static void Quest()
        {
            WithExtraDefine(UnityEditor.Build.NamedBuildTarget.Android, "YA_QUEST_BUILD",
                () => Build(BuildTarget.Android, "Quest/Yaoron-Quest.apk"));
        }

        [MenuItem("Yaoron/ビルド/iOS", priority = 44)]
        public static void Ios() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("Yaoron/ビルド/WebGL", priority = 45)]
        public static void WebGl() => Build(BuildTarget.WebGL, "WebGL");

        static void Build(BuildTarget target, string relativePath)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!CheckPlatform(target, group)) return;

            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogError("[Yaoron] Build Settings にシーンがありません。先にシーンを生成してください。");
                return;
            }

            var output = Path.Combine(OutputRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(scenes, s => s.path),
                target = target,
                targetGroup = group,
                locationPathName = output,
                options = BuildOptions.None,
            };

            WithFallbackBackend(target, group, () =>
            {
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result == BuildResult.Succeeded)
                    Debug.Log($"[Yaoron] ビルド成功: {output} ({summary.totalSize / (1024 * 1024)} MB)");
                else
                    Debug.LogError($"[Yaoron] ビルド失敗: {summary.result} / エラー {summary.totalErrors} 件");
            });
        }

        /// <summary>
        /// モジュール未インストールと、そもそもこの OS では作れない組み合わせを先に弾く。
        /// BuildPipeline に渡してからエラーになると原因が分かりにくいため。
        /// </summary>
        static bool CheckPlatform(BuildTarget target, BuildTargetGroup group)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[Yaoron] {target} のビルドモジュールが入っていません。" +
                               "Unity Hub の「モジュールを加える」から追加してください。");
                return false;
            }

            // iOS は Xcode が要るので macOS 上でしかビルドできない。
            if (target == BuildTarget.iOS && Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogError("[Yaoron] iOS のビルドは macOS 上でのみ可能です (Xcode プロジェクトの生成に Xcode が必要)。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// macOS の IL2CPP ビルドは Mac 上でしか行えない (Windows 側のモジュールは Mono のみ)。
        /// 動作確認用に Mono で通し、ビルド後に元のバックエンドへ戻す。
        /// 配布ビルドは設計書 §11 のとおり Mac 上で IL2CPP で作ること。
        /// </summary>
        static void WithFallbackBackend(BuildTarget target, BuildTargetGroup group, Action build)
        {
            bool crossBuildingMac = target == BuildTarget.StandaloneOSX
                                    && Application.platform != RuntimePlatform.OSXEditor;
            if (!crossBuildingMac) { build(); return; }

            var named = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            var original = PlayerSettings.GetScriptingBackend(named);
            if (original == ScriptingImplementation.Mono2x) { build(); return; }

            Debug.LogWarning("[Yaoron] Windows からの macOS ビルドは IL2CPP に対応していないため、" +
                             "今回だけ Mono でビルドします。配布用は Mac 上で IL2CPP ビルドしてください。");
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.Mono2x);
            try { build(); }
            finally { PlayerSettings.SetScriptingBackend(named, original); }
        }

        static void WithExtraDefine(UnityEditor.Build.NamedBuildTarget target, string define, Action action)
        {
            var original = PlayerSettings.GetScriptingDefineSymbols(target);
            if (!original.Contains(define))
                PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(original) ? define : original + ";" + define);
            try { action(); }
            finally { PlayerSettings.SetScriptingDefineSymbols(target, original); }
        }
    }
}
