using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 設計書 §11 のビルド設定のうち、コードで確実に入れられるものを適用する。
    /// (プラットフォームモジュール未インストールの環境では該当箇所を黙って飛ばす)
    /// </summary>
    public static class YaoronPlayerSettings
    {
        [MenuItem("Yaoron/セットアップ/プレイヤー設定を適用", priority = 20)]
        public static void ApplyAll()
        {
            ApplyCommon();
            ApplyDesktop();
            ApplyAndroid();
            ApplyIos();
            ApplyWebGl();
            AssetDatabase.SaveAssets();
            Debug.Log("[Yaoron] プレイヤー設定を適用しました。");
        }

        static void ApplyCommon()
        {
            PlayerSettings.companyName = string.IsNullOrEmpty(PlayerSettings.companyName)
                ? "Yaoron" : PlayerSettings.companyName;
            PlayerSettings.productName = "Yaoron";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gcIncremental = true;

            // 旧 Input と Input System の両方を有効にする。仮想スティックの Touch 読み取りが
            // 旧 API 依存のため、片方だけにすると壊れる。
            SetActiveInputHandling(2);
        }

        static void ApplyDesktop()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
            PlayerSettings.macOS.buildNumber = "1";
            // macOS アプリは NSMicrophoneUsageDescription が無いと起動時に落ちる (設計書 §3)。
            PlayerSettings.macOS.microphoneUsageDescription = "近くの参加者と会話するためにマイクを使用します。";
        }

        static void ApplyAndroid()
        {
            try
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.forceInternetPermission = true;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
                // RECORD_AUDIO はマイクを使うと Unity が自動で付与するが、実行時許可は自前で取る。
            }
            catch (System.Exception e) { Debug.LogWarning($"[Yaoron] Android 設定を適用できません: {e.Message}"); }
        }

        static void ApplyIos()
        {
            try
            {
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                PlayerSettings.iOS.microphoneUsageDescription = "近くの参加者と会話するためにマイクを使用します。";
                PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Yaoron] iOS 設定を適用できません: {e.Message}"); }
        }

        static void ApplyWebGl()
        {
            try
            {
                PlayerSettings.WebGL.memorySize = 512;                       // 設計書 §11
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.threadsSupport = false;                 // スレッド不可
                PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            }
            catch (System.Exception e) { Debug.LogWarning($"[Yaoron] WebGL 設定を適用できません: {e.Message}"); }
        }

        /// <summary>0 = 旧 Input のみ / 1 = Input System のみ / 2 = 両方。</summary>
        static void SetActiveInputHandling(int mode)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var settings = new SerializedObject(assets[0]);
            var property = settings.FindProperty("activeInputHandler");
            if (property == null) return;
            if (property.intValue == mode) return;
            property.intValue = mode;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
