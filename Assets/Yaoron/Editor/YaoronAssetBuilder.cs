using Yaoron.Avatar;
using Yaoron.Core;
using Yaoron.Inputs;
using Yaoron.Net;
using Yaoron.UI;
using Yaoron.Voice;
using Yaoron.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Yaoron.EditorTools
{
    /// <summary>
    /// 設計書 §5 のプレハブとシーンをコードから組み立てる。
    /// 手作業の配線ミスを避けたいのと、Normcore / UniVRM の導入状況で構成が変わるため、
    /// 生成をスクリプト化して何度でも作り直せるようにしている。
    /// </summary>
    public static class YaoronAssetBuilder
    {
        const string Root = "Assets/Yaoron";
        const string SettingsFolder = Root + "/Settings";
        const string ResourcesFolder = Root + "/Resources";
        const string ScenesFolder = Root + "/Scenes";

        public const string AvatarPrefabPath = ResourcesFolder + "/Avatar.prefab";
        public const string BootScenePath = ScenesFolder + "/Boot.unity";
        public const string WorldScenePath = ScenesFolder + "/World_Plaza.unity";
        public const string ConfigPath = SettingsFolder + "/NormcoreConfig.asset";
        public const string CatalogPath = SettingsFolder + "/AvatarCatalog.asset";

        [MenuItem("Yaoron/セットアップ/すべて生成", priority = 10)]
        public static void BuildAll()
        {
            CreateSettings();
            CreateAvatarPrefab();
            CreateScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Yaoron] プレハブとシーンを生成しました。Boot シーンから再生してください。");
        }

        // ------------------------------------------------------------ 設定アセット

        [MenuItem("Yaoron/セットアップ/設定アセットを作成", priority = 11)]
        public static NormcoreConfig CreateSettings()
        {
            YaEditorUtil.EnsureFolder(SettingsFolder);
            var config = YaEditorUtil.CreateAsset<NormcoreConfig>(ConfigPath);
            YaEditorUtil.CreateAsset<AvatarCatalog>(CatalogPath);
            EditorUtility.SetDirty(config);
            return config;
        }

        // ------------------------------------------------------------ アバタープレハブ

        /// <summary>
        /// Realtime.Instantiate は Resources 下の名前で解決するので、必ず Resources に置く (設計書 §5)。
        /// Normcore 導入済みなら RealtimeView / RealtimeTransform / RealtimeAvatarVoice も付ける。
        /// </summary>
        [MenuItem("Yaoron/セットアップ/アバタープレハブを作成", priority = 12)]
        public static GameObject CreateAvatarPrefab()
        {
            YaEditorUtil.EnsureFolder(ResourcesFolder);

            var root = new GameObject("Avatar");
            var head = YaEditorUtil.Child(root, "Head");
            var leftHand = YaEditorUtil.Child(root, "LeftHand");
            var rightHand = YaEditorUtil.Child(root, "RightHand");
            var model = YaEditorUtil.Child(root, "Model");
            var placeholderRoot = YaEditorUtil.Child(root, "Placeholder");

            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            leftHand.transform.localPosition = new Vector3(-0.25f, 1.1f, 0.2f);
            rightHand.transform.localPosition = new Vector3(0.25f, 1.1f, 0.2f);

            // --- 仮表示のカプセル (VRM が来るまで、あるいは読み込み失敗時に残る)
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Capsule";
            capsule.transform.SetParent(placeholderRoot.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            Object.DestroyImmediate(capsule.GetComponent<Collider>());
            var placeholder = placeholderRoot.AddComponent<AvatarPlaceholder>();
            YaEditorUtil.SetField(placeholder, "_visual", placeholderRoot);

            // --- Normcore (未導入なら静かに飛ばす)
            YaEditorUtil.AddComponentByName(root, "Normal.Realtime.RealtimeView");
            YaEditorUtil.AddComponentByName(root, "Normal.Realtime.RealtimeTransform");
            YaEditorUtil.AddComponentByName(head, "Normal.Realtime.RealtimeTransform");
            YaEditorUtil.AddComponentByName(leftHand, "Normal.Realtime.RealtimeTransform");
            YaEditorUtil.AddComponentByName(rightHand, "Normal.Realtime.RealtimeTransform");

            // --- 音声 (設計書 §7: 頭付近に置いた 3D AudioSource を SDK が使う)
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;   // Web では YaAvatar が実行時に 2D へ落とす (ADR-4)
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 1f;
            audio.maxDistance = 20f;
            YaEditorUtil.AddComponentByName(root, "Normal.Realtime.RealtimeAvatarVoice");

            // --- Yaoron 本体
            var avatar = root.AddComponent<YaAvatar>();
            var localDriver = root.AddComponent<AvatarLocalDriver>();
            var remoteDriver = root.AddComponent<AvatarRemoteDriver>();
            var poseSolver = root.AddComponent<AvatarPoseSolver>();
            YaEditorUtil.AddComponentByName(root, "Yaoron.Avatar.AvatarSync");

            YaEditorUtil.SetField(avatar, "_root", root.transform);
            YaEditorUtil.SetField(avatar, "_head", head.transform);
            YaEditorUtil.SetField(avatar, "_leftHand", leftHand.transform);
            YaEditorUtil.SetField(avatar, "_rightHand", rightHand.transform);
            YaEditorUtil.SetField(avatar, "_modelParent", model.transform);
            YaEditorUtil.SetField(avatar, "_placeholder", placeholder);
            YaEditorUtil.SetField(avatar, "_localDriver", localDriver);
            YaEditorUtil.SetField(avatar, "_remoteDriver", remoteDriver);
            YaEditorUtil.SetField(avatar, "_poseSolver", poseSolver);
            YaEditorUtil.SetField(localDriver, "_avatar", avatar);
            YaEditorUtil.SetField(remoteDriver, "_avatar", avatar);

            // --- 名札と発話インジケータ
            BuildNameplate(root, avatar);

            // RealtimeView は OnValidate で同期対象コンポーネントを集めるので、保存前に一度走らせる。
            var realtimeView = YaEditorUtil.FindType("Normal.Realtime.RealtimeView");
            if (realtimeView != null) YaEditorUtil.InvokeValidate(root.GetComponent(realtimeView));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, AvatarPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Yaoron] アバタープレハブを作成しました: {AvatarPrefabPath}");
            return prefab;
        }

        static void BuildNameplate(GameObject root, YaAvatar avatar)
        {
            var nameplate = YaEditorUtil.Child(root, "Nameplate");
            nameplate.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            nameplate.transform.localScale = Vector3.one * 0.01f;

            var canvas = nameplate.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = nameplate.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 50f);

            var labelGo = YaEditorUtil.Child(nameplate, "Label");
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 28;
            label.color = Color.white;
            label.text = "ゲスト";
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(200f, 40f);

            var iconGo = YaEditorUtil.Child(nameplate, "SpeakingIcon");
            var icon = iconGo.AddComponent<Image>();
            icon.color = new Color(0.4f, 0.9f, 0.5f, 0.9f);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(16f, 16f);
            iconRect.anchoredPosition = new Vector2(-110f, 0f);

            var view = nameplate.AddComponent<NameplateView>();
            YaEditorUtil.SetField(view, "_avatar", avatar);
            YaEditorUtil.SetField(view, "_label", label);
            YaEditorUtil.SetField(view, "_canvas", canvas);

            var indicator = nameplate.AddComponent<VoiceIndicator>();
            YaEditorUtil.SetField(indicator, "_avatar", avatar);
            YaEditorUtil.SetField(indicator, "_icon", iconGo);
        }

        // ------------------------------------------------------------ シーン

        [MenuItem("Yaoron/セットアップ/シーンを作成", priority = 13)]
        public static void CreateScenes()
        {
            YaEditorUtil.EnsureFolder(ScenesFolder);
            CreateWorldScene();
            CreateBootScene();
            RegisterScenesInBuildSettings();
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var boot = new GameObject("[AppBootstrap]");
            boot.AddComponent<AppBootstrap>();

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        static void CreateWorldScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 既定のカメラはリグ側で用意するので消す。
            var defaultCamera = Object.FindFirstObjectByType<Camera>();
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

            BuildGround();

            var config = AssetDatabase.LoadAssetAtPath<NormcoreConfig>(ConfigPath);
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            var avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);

            // --- ネットワーク / セッション
            var netGo = new GameObject("[Session]");
            var realtime = YaEditorUtil.AddComponentByName(netGo, "Normal.Realtime.Realtime");
            ConfigureRealtime(realtime);
            var session = netGo.AddComponent<SessionController>();
            var capacity = netGo.AddComponent<RoomCapacityGuard>();
            YaEditorUtil.SetField(session, "_config", config);
            YaEditorUtil.SetField(session, "_capacityGuard", capacity);
            YaEditorUtil.SetBool(session, "_joinOnStart", false);   // 入室は JoinPanel から
            YaEditorUtil.SetField(capacity, "_config", config);

            // --- アバター
            var avatarGo = new GameObject("[Avatars]");
            var manager = avatarGo.AddComponent<YaAvatarManager>();
            var loader = avatarGo.AddComponent<AvatarLoader>();
            YaEditorUtil.SetField(manager, "_session", session);
            YaEditorUtil.SetField(manager, "_config", config);
            YaEditorUtil.SetField(manager, "_offlineAvatarPrefab", avatarPrefab);
            YaEditorUtil.SetField(loader, "_catalog", catalog);

            // --- 音声
            var voiceGo = new GameObject("[Voice]");
            voiceGo.AddComponent<NormcoreVoiceService>();
            var culler = voiceGo.AddComponent<VoiceRangeCuller>();
            YaEditorUtil.SetFloat(culler, "_radius", config != null ? config.listenRadius : 20f);
            YaEditorUtil.SetFloat(culler, "_hysteresis", config != null ? config.listenHysteresis : 2f);

            // --- リグ
            var rigsGo = new GameObject("[Rigs]");
            var desktop = BuildDesktopRig(rigsGo, false);
            var mobile = BuildDesktopRig(rigsGo, true);
            mobile.SetActive(false);       // AudioListener が二重にならないよう既定は無効
            var xr = BuildXrRig(rigsGo);

            var detection = rigsGo.AddComponent<VRDetection>();
            YaEditorUtil.SetField(detection, "_desktopRig", desktop);
            YaEditorUtil.SetField(detection, "_mobileRig", mobile);
            YaEditorUtil.SetField(detection, "_xrRig", xr);

            // --- UI
            BuildUi(session, capacity, loader, mobile);

            EditorSceneManager.SaveScene(scene, WorldScenePath);
        }

        /// <summary>
        /// Realtime の既定は「Start で自動接続」なので切る。入室は SessionController が
        /// マイク権限のあとに Quickmatch で行う。App Key のアセットもここで割り当てておく。
        /// </summary>
        static void ConfigureRealtime(Component realtime)
        {
            if (realtime == null) return;
            YaEditorUtil.SetBool(realtime, "_joinRoomOnStart", false);

            var settingsType = YaEditorUtil.FindType("Normal.NormcoreAppSettings");
            if (settingsType == null) return;

            foreach (var guid in AssetDatabase.FindAssets("t:NormcoreAppSettings"))
            {
                var asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), settingsType);
                if (asset == null) continue;
                YaEditorUtil.SetField(realtime, "_normcoreAppSettings", asset);
                return;
            }
            Debug.LogWarning("[Yaoron] NormcoreAppSettings が見つかりません。Normcore の App Key を設定してください。");
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);   // 40 m 四方
            ground.isStatic = true;
        }

        static GameObject BuildDesktopRig(GameObject parent, bool mobile)
        {
            var rig = YaEditorUtil.Child(parent, mobile ? "MobileRig" : "DesktopRig");
            rig.transform.position = new Vector3(0f, 0f, 0f);

            var controller = rig.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.25f;
            controller.center = new Vector3(0f, 0.85f, 0f);

            // 頭 (同期される位置) とカメラは別の GameObject にする。
            // 三人称でカメラを引いたときに、他人から見える頭まで下がってしまわないように。
            var head = YaEditorUtil.Child(rig, "Head");
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraGo = YaEditorUtil.Child(head, "Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraGo.AddComponent<AudioListener>();

            var playerRig = rig.AddComponent<PlayerRig>();
            YaEditorUtil.SetField(playerRig, "_camera", camera);
            YaEditorUtil.SetField(playerRig, "_head", head.transform);

            rig.AddComponent<DesktopInput>();
            if (mobile) rig.AddComponent<TouchInput>();

            return rig;
        }

        static GameObject BuildXrRig(GameObject parent)
        {
            var rig = YaEditorUtil.Child(parent, "XRRig");
            var controller = rig.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.25f;
            controller.center = new Vector3(0f, 0.85f, 0f);

            var offset = YaEditorUtil.Child(rig, "Camera Offset");
            var head = YaEditorUtil.Child(offset, "Main Camera");
            var camera = head.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            head.AddComponent<AudioListener>();

            var left = YaEditorUtil.Child(offset, "LeftHand");
            var right = YaEditorUtil.Child(offset, "RightHand");

            var xrRig = rig.AddComponent<XRPlayerRig>();
            YaEditorUtil.SetField(xrRig, "_cameraOffset", offset.transform);
            YaEditorUtil.SetField(xrRig, "_camera", camera);
            YaEditorUtil.SetField(xrRig, "_leftHand", left.transform);
            YaEditorUtil.SetField(xrRig, "_rightHand", right.transform);
            rig.AddComponent<XRInput>();

            rig.SetActive(false);
            return rig;
        }

        // ------------------------------------------------------------ UI

        static void BuildUi(SessionController session, RoomCapacityGuard capacity, AvatarLoader loader, GameObject mobileRig)
        {
            var canvasGo = new GameObject("[UI]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            var permissions = canvasGo.AddComponent<PermissionFlow>();
            BuildJoinPanel(canvasGo, session, loader, permissions);
            BuildHud(canvasGo, session, capacity);
            BuildTouchStick(canvasGo, mobileRig);
        }

        static Text CreateText(GameObject parent, string name, string content, int size, TextAnchor anchor,
                               Vector2 size2, Vector2 position)
        {
            var go = YaEditorUtil.Child(parent, name);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size2;
            rect.anchoredPosition = position;
            return text;
        }

        static void BuildJoinPanel(GameObject canvas, SessionController session, AvatarLoader loader, PermissionFlow permissions)
        {
            var panel = YaEditorUtil.Child(canvas, "JoinPanel");
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            var background = panel.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);

            CreateText(panel, "Title", "Yaoron", 42, TextAnchor.MiddleCenter,
                new Vector2(600f, 60f), new Vector2(0f, 160f));

            var resources = new DefaultControls.Resources();

            var nameField = DefaultControls.CreateInputField(resources);
            nameField.name = "NameField";
            nameField.transform.SetParent(panel.transform, false);
            var nameRect = nameField.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(360f, 40f);
            nameRect.anchoredPosition = new Vector2(0f, 70f);
            var input = nameField.GetComponent<InputField>();
            input.characterLimit = 16;
            if (input.placeholder is Text placeholder) placeholder.text = "表示名を入力";

            var dropdownGo = DefaultControls.CreateDropdown(resources);
            dropdownGo.name = "AvatarDropdown";
            dropdownGo.transform.SetParent(panel.transform, false);
            var dropdownRect = dropdownGo.GetComponent<RectTransform>();
            dropdownRect.sizeDelta = new Vector2(360f, 40f);
            dropdownRect.anchoredPosition = new Vector2(0f, 10f);
            var dropdown = dropdownGo.GetComponent<Dropdown>();

            var joinGo = DefaultControls.CreateButton(resources);
            joinGo.name = "JoinButton";
            joinGo.transform.SetParent(panel.transform, false);
            var joinRect = joinGo.GetComponent<RectTransform>();
            joinRect.sizeDelta = new Vector2(200f, 48f);
            joinRect.anchoredPosition = new Vector2(0f, -60f);
            var joinButton = joinGo.GetComponent<Button>();
            var joinLabel = joinGo.GetComponentInChildren<Text>();
            if (joinLabel != null) joinLabel.text = "入室";

            var status = CreateText(panel, "Status", string.Empty, 20, TextAnchor.MiddleCenter,
                new Vector2(600f, 30f), new Vector2(0f, -120f));

            var denied = YaEditorUtil.Child(panel, "DeniedPanel");
            denied.AddComponent<RectTransform>();
            var deniedText = CreateText(denied, "Message", string.Empty, 18, TextAnchor.MiddleCenter,
                new Vector2(600f, 60f), new Vector2(0f, -170f));
            denied.SetActive(false);

            YaEditorUtil.SetField(permissions, "_deniedPanel", denied);
            YaEditorUtil.SetField(permissions, "_deniedMessage", deniedText);

            var join = canvas.AddComponent<JoinPanel>();
            YaEditorUtil.SetField(join, "_panel", panel);
            YaEditorUtil.SetField(join, "_nameField", input);
            YaEditorUtil.SetField(join, "_avatarDropdown", dropdown);
            YaEditorUtil.SetField(join, "_joinButton", joinButton);
            YaEditorUtil.SetField(join, "_status", status);
            YaEditorUtil.SetField(join, "_session", session);
            YaEditorUtil.SetField(join, "_loader", loader);
            YaEditorUtil.SetField(join, "_permissions", permissions);
        }

        static void BuildHud(GameObject canvas, SessionController session, RoomCapacityGuard capacity)
        {
            var hud = YaEditorUtil.Child(canvas, "Hud");
            var hudRect = hud.AddComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 1f);
            hudRect.anchorMax = new Vector2(0f, 1f);
            hudRect.pivot = new Vector2(0f, 1f);
            hudRect.anchoredPosition = new Vector2(16f, -16f);

            var room = CreateText(hud, "Room", "-", 20, TextAnchor.UpperLeft,
                new Vector2(400f, 28f), new Vector2(200f, -14f));
            var state = CreateText(hud, "State", "未接続", 18, TextAnchor.UpperLeft,
                new Vector2(400f, 24f), new Vector2(200f, -44f));

            var resources = new DefaultControls.Resources();

            var muteGo = DefaultControls.CreateButton(resources);
            muteGo.name = "MuteButton";
            muteGo.transform.SetParent(hud.transform, false);
            var muteRect = muteGo.GetComponent<RectTransform>();
            muteRect.sizeDelta = new Vector2(140f, 36f);
            muteRect.anchoredPosition = new Vector2(90f, -90f);
            var muteLabel = muteGo.GetComponentInChildren<Text>();
            if (muteLabel != null) muteLabel.text = "送信中";

            var leaveGo = DefaultControls.CreateButton(resources);
            leaveGo.name = "LeaveButton";
            leaveGo.transform.SetParent(hud.transform, false);
            var leaveRect = leaveGo.GetComponent<RectTransform>();
            leaveRect.sizeDelta = new Vector2(100f, 36f);
            leaveRect.anchoredPosition = new Vector2(250f, -90f);
            var leaveLabel = leaveGo.GetComponentInChildren<Text>();
            if (leaveLabel != null) leaveLabel.text = "退室";

            var indicatorGo = YaEditorUtil.Child(hud, "SpeakingIndicator");
            var indicator = indicatorGo.AddComponent<Image>();
            indicator.color = new Color(0.4f, 0.9f, 0.5f, 0.2f);
            var indicatorRect = indicatorGo.GetComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(18f, 18f);
            indicatorRect.anchoredPosition = new Vector2(10f, -90f);

            var view = canvas.AddComponent<HudView>();
            YaEditorUtil.SetField(view, "_session", session);
            YaEditorUtil.SetField(view, "_capacity", capacity);
            YaEditorUtil.SetField(view, "_roomLabel", room);
            YaEditorUtil.SetField(view, "_stateLabel", state);
            YaEditorUtil.SetField(view, "_muteButton", muteGo.GetComponent<Button>());
            YaEditorUtil.SetField(view, "_muteLabel", muteLabel);
            YaEditorUtil.SetField(view, "_speakingIndicator", indicator);
            YaEditorUtil.SetField(view, "_leaveButton", leaveGo.GetComponent<Button>());
        }

        static void BuildTouchStick(GameObject canvas, GameObject mobileRig)
        {
            var stick = YaEditorUtil.Child(canvas, "TouchStick");
            var stickRect = stick.AddComponent<RectTransform>();
            stickRect.anchorMin = stickRect.anchorMax = new Vector2(0f, 0f);
            stickRect.pivot = new Vector2(0.5f, 0.5f);
            stickRect.anchoredPosition = new Vector2(140f, 140f);
            stickRect.sizeDelta = new Vector2(180f, 180f);
            var background = stick.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.12f);

            var handleGo = YaEditorUtil.Child(stick, "Handle");
            var handle = handleGo.AddComponent<Image>();
            handle.color = new Color(1f, 1f, 1f, 0.35f);
            var handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(70f, 70f);

            var view = stick.AddComponent<TouchStickView>();
            YaEditorUtil.SetField(view, "_background", stickRect);
            YaEditorUtil.SetField(view, "_handle", handleRect);
            if (mobileRig != null)
            {
                var touch = mobileRig.GetComponent<TouchInput>();
                if (touch != null) YaEditorUtil.SetField(view, "_target", touch);
            }

            // 非モバイルでは TouchStickView 自身が実行時に自分を消す。
        }

        static void RegisterScenesInBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(WorldScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
