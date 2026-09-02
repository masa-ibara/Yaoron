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
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = output,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[Yaoron] ビルド成功: {output} ({summary.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"[Yaoron] ビルド失敗: {summary.result} / エラー {summary.totalErrors} 件");
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
