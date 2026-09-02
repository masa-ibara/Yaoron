using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 導入手順をひとつの窓にまとめたセットアップ画面。
    /// 依存パッケージ → プロジェクト設定 → プレハブ / シーン生成、の順に押していけば動く状態になる。
    /// </summary>
    public class YaoronSetupWindow : EditorWindow
    {
        const string UniVrmTagKey = "Yaoron.UniVrmTag";

        string _uniVrmTag = "v0.131.2";
        Vector2 _scroll;

        [MenuItem("Yaoron/セットアップ ウィンドウ", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<YaoronSetupWindow>("Yaoron セットアップ");
            window.minSize = new Vector2(460f, 520f);
        }

        void OnEnable() => _uniVrmTag = EditorPrefs.GetString(UniVrmTagKey, _uniVrmTag);

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("1. 依存パッケージ", EditorStyles.boldLabel);
            DrawStatus();
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(YaoronPackages.Busy))
            {
                if (GUILayout.Button("Normcore を導入 (同期 + 音声)"))
                    YaoronPackages.Install(new[] { YaoronPackages.Normcore });

                EditorGUILayout.BeginHorizontal();
                _uniVrmTag = EditorGUILayout.TextField("UniVRM タグ", _uniVrmTag);
                if (GUILayout.Button("UniVRM を導入", GUILayout.Width(120f)))
                {
                    EditorPrefs.SetString(UniVrmTagKey, _uniVrmTag);
                    YaoronPackages.Install(YaoronPackages.UniVrmGit(_uniVrmTag));
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(
                    "UniVRM は git 経由で 2 パッケージ (UniGLTF / VRM10) をまとめて入れます。" +
                    "タグは vrm-c/UniVRM のリリースに合わせてください。git が使えない環境では " +
                    "公式の .unitypackage を Assets 直下に展開しても構いません。",
                    MessageType.Info);

                if (GUILayout.Button("Input System を導入"))
                    YaoronPackages.Install(new[] { YaoronPackages.InputSystem });

                if (GUILayout.Button("XR (OpenXR + XR Management) を導入"))
                    YaoronPackages.Install(new[]
                    {
                        YaoronPackages.XrManagement,
                        YaoronPackages.OpenXr,
                    });

                if (GUILayout.Button("UniTask を導入 (任意)"))
                    YaoronPackages.Install(new[] { YaoronPackages.UniTaskGit });

                if (GUILayout.Button("すべてまとめて導入"))
                {
                    EditorPrefs.SetString(UniVrmTagKey, _uniVrmTag);
                    var all = new List<string>
                    {
                        YaoronPackages.Normcore,
                        YaoronPackages.InputSystem,
                        YaoronPackages.XrManagement,
                        YaoronPackages.OpenXr,
                    };
                    all.AddRange(YaoronPackages.UniVrmGit(_uniVrmTag));
                    YaoronPackages.Install(all);
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("2. プロジェクト設定", EditorStyles.boldLabel);
            if (GUILayout.Button("プレイヤー設定を適用 (マイク権限・入力・色空間ほか)"))
                YaoronPlayerSettings.ApplyAll();
            if (GUILayout.Button("依存パッケージの定義を再検出"))
                YaoronDefines.Sync();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("3. プレハブとシーン", EditorStyles.boldLabel);
            if (GUILayout.Button("すべて生成 (設定 / アバター / シーン)"))
                YaoronAssetBuilder.BuildAll();
            if (GUILayout.Button("アバタープレハブだけ作り直す"))
                YaoronAssetBuilder.CreateAvatarPrefab();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("4. 残りの手作業", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "・Normcore の App Key を Normcore の設定アセットに入力する\n" +
                "・AvatarCatalog にプリセット VRM の ID と URL を登録する\n" +
                "・Normcore 導入後は Avatar プレハブを一度開き、RealtimeView に\n" +
                "  子の RealtimeTransform / RealtimeComponent が登録されているか確認する",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void DrawStatus()
        {
            Row("Normcore", "Normal.Realtime.Realtime");
            Row("UniVRM (VRM 1.0)", "UniVRM10.Vrm10");
            Row("Input System", "UnityEngine.InputSystem.Keyboard");
            Row("UniTask", "Cysharp.Threading.Tasks.UniTask");
            Row("OpenXR", "UnityEngine.XR.OpenXR.OpenXRSettings");
        }

        static void Row(string label, string probeType)
        {
            bool present = YaEditorUtil.FindType(probeType) != null;
            EditorGUILayout.LabelField(label, present ? "導入済み" : "未導入");
        }
    }
}
