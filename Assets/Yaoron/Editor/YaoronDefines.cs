using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 依存パッケージの有無をスクリプト定義シンボルに反映する。
    /// Yaoron のランタイムコードはこのシンボルで機能を出し入れするので、
    /// Normcore / UniVRM が未導入でもプロジェクトはコンパイルが通り、導入した瞬間に有効になる。
    /// asmdef の versionDefines を使わないのは、パッケージ未導入の asmdef 参照が
    /// そのままコンパイルエラーになってしまうため。
    /// </summary>
    [InitializeOnLoad]
    public static class YaoronDefines
    {
        /// <summary>定義シンボル → その存在を示す型のフル名。</summary>
        static readonly Dictionary<string, string> Probes = new Dictionary<string, string>
        {
            { "YA_NORMCORE",    "Normal.Realtime.Realtime" },
            { "YA_VRM",         "UniVRM10.Vrm10" },
            { "YA_UNITASK",     "Cysharp.Threading.Tasks.UniTask" },
            { "YA_INPUTSYSTEM", "UnityEngine.InputSystem.Keyboard" },
        };

        static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.WebGL,
        };

        static YaoronDefines() => EditorApplication.delayCall += Sync;

        [MenuItem("Yaoron/依存パッケージの定義を再検出", priority = 30)]
        public static void Sync()
        {
            var present = new HashSet<string>();
            foreach (var pair in Probes)
                if (HasType(pair.Value)) present.Add(pair.Key);

            foreach (var target in Targets) Apply(target, present);
        }

        static void Apply(NamedBuildTarget target, HashSet<string> present)
        {
            string current;
            try { current = PlayerSettings.GetScriptingDefineSymbols(target); }
            catch (Exception) { return; }   // そのプラットフォームのモジュールが未インストール

            var symbols = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            bool changed = false;

            foreach (var symbol in Probes.Keys)
            {
                bool wanted = present.Contains(symbol);
                bool has = symbols.Contains(symbol);
                if (wanted == has) continue;
                if (wanted) symbols.Add(symbol); else symbols.Remove(symbol);
                changed = true;
            }

            if (!changed) return;
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            Debug.Log($"[Yaoron] {target.TargetName} の定義を更新: {string.Join(";", symbols)}");
        }

        static bool HasType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (assembly.GetType(fullName, false) != null) return true; }
                catch (Exception) { /* 読めないアセンブリは無視 */ }
            }
            return false;
        }
    }
}
