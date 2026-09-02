# Yaoron 実装メモ

設計書「Yaoron 設計書 (Normcore 版) Rev 0.1」の実装。
Unity 6 (6000.2.6f2) / URP / **Normcore 3.5.2** / **UniVRM 0.131.2**。

> 設計書は Normcore 2.x を前提に書かれているが、導入されたのは 3.5.2。
> 3.0 で入った **Quickmatch**(定員付きの自動振り分け)と **AutoReconnect** により、
> §9 の「人数上限が SDK に無いのでアプリ側で判定する」「セッション一覧 API が無い」という
> 前提が実質的に解消されている。詳細は §4 の差分を参照。

外部パッケージ (Normcore・UniVRM) が未導入でもプロジェクトはコンパイルが通り、
**オフライン単独モード**として起動できる。パッケージを入れると定義シンボルが自動で立ち、
同期・音声・VRM が有効になる。

---

## 1. セットアップ

メニュー **Yaoron ▸ セットアップ ウィンドウ** を開き、上から順に押す。

| 手順 | 内容 |
|------|------|
| 1. 依存パッケージ | Normcore / UniVRM / Input System / XR (OpenXR) / UniTask を Package Manager 経由で導入。Normcore のスコープ付きレジストリは `Packages/manifest.json` に登録済み。UniVRM は git 経由で UniGLTF・VRM10 の 2 つを同時に入れる (0.128 より前は VRMShaders も必要)。**導入済みなら押す必要はない**。 |
| 2. プロジェクト設定 | マイク権限の説明文、IL2CPP、ストリッピング、WebGL 512 MB、Active Input Handling = Both などを適用 (設計書 §11)。 |
| 3. プレハブとシーン | `Resources/Avatar.prefab`、`Scenes/Boot.unity`、`Scenes/World_Plaza.unity`、設定アセット 2 種を生成し、Build Settings に登録。 |
| 4. 手作業 | Normcore の App Key 入力、`AvatarCatalog` への VRM 登録、Normcore 導入後の Avatar プレハブ確認。 |

導入状況は同ウィンドウ上部に「導入済み / 未導入」で出る。手動で再検出したいときは
**Yaoron ▸ 依存パッケージの定義を再検出**。

### 定義シンボル

| シンボル | 立つ条件 | 効果 |
|----------|----------|------|
| `YA_NORMCORE` | Normcore 導入 | ルーム接続・状態同期・音声が有効。無いとオフライン単独モード。 |
| `YA_VRM` | UniVRM (VRM10) 導入 | VRM の読み込みと表情・口パク。無いとカプセルのまま。 |
| `YA_INPUTSYSTEM` | Input System 導入 | キーボード / マウス / タッチを新 API で読む。無ければ旧 Input Manager。asmdef の `versionDefines` からも立つ。 |
| `YA_UNITASK` | UniTask 導入 | 予約 (現状のコードは UniTask 無しでも WebGL 安全)。 |
| `YA_QUEST_BUILD` | Quest ビルド時のみ | VR 判定を固定する。`Yaoron ▸ ビルド ▸ Quest` が自動で付ける。 |

## 2. 動かす

1. `Assets/Yaoron/Scenes/Boot.unity` を開いて再生。`World_Plaza` に遷移する。
2. 入室パネルで表示名を入れて「入室」。マイク権限 → 接続 → アバター生成の順に進む。
3. WASD + 右ドラッグで移動と視点。`T` = PTT、`M` = ミュート切替。

Normcore 未導入時は接続をスキップしてローカルにアバターを 1 体出すだけになる。
リグ・入力・UI・名札・LOD の確認はこの状態でもできる。

## 3. 構成

```
Core/    PlatformProfile (VR / モバイル / Web 判定), SettingsStore, YaServices, AppBootstrap, YaAwait
Net/     SessionController (Quickmatch 入室・再接続・バックグラウンド切断), RoomCapacityGuard, LobbyDirectory
Avatar/  YaAvatarManager (生成と台帳), YaAvatar (器), AvatarState (状態),
         AvatarSync (Normcore 橋渡し), AvatarLocalDriver / AvatarRemoteDriver,
         AvatarPoseSolver (頭の向き・3点IK・LOD), TwoBoneIk, AvatarLoader, AvatarCatalog
Voice/   NormcoreVoiceService (ミュート/PTT/発話量), VoiceRangeCuller (20 m 聴取範囲), VoiceIndicator
Input/   IInputSource, DesktopInput, TouchInput, PlayerRig (非VR の身体)
XR/      XRPlayerRig (XR Origin 相当), XRInput, VRDetection (リグ選択)
UI/      JoinPanel, HudView, NameplateView, PermissionFlow, TouchStickView
Editor/  セットアップウィンドウ, 定義シンボル同期, パッケージ導入, プレハブ/シーン生成, ビルド, プレイヤー設定
```

同期する状態は設計書 §6 のとおり `AvatarStateModel`
(avatarId / displayName / isVR / locomotion / moveDir / expression) と、
ルート・頭・両手の `RealtimeTransform` のみ。VRM 本体は流さず ID だけを配り、
各クライアントが HTTPS から取得する (ADR-3)。

## 4. 設計書との差分

実装にあたって設計書から意図的に変えた点。いずれも設計の狙いは変えていない。

1. **`RealtimeAvatarManager` / `RealtimeAvatar` をコピーせず、同等品を自前で書いた** (§5)。
   Normcore 3 では両者はパッケージに .cs で同梱されているのでコピー自体は可能だが、
   複製するとパッケージ更新のたびに差分を取り込む必要が出る。非VR 用の状態
   (locomotion / expression / avatarId) を足すことを考えると自前実装のほうが素直で、
   依存する Normcore の API も `Realtime` / `RealtimeView` / `RealtimeTransform` /
   `RealtimeComponent` / `RealtimeAvatarVoice` の 5 つに収まっている。

2. **Assembly Definition は設計書どおり分割済み** (§5)。
   `Yaoron.Core / Net / Avatar / Voice / Inputs / XR / UI` の 7 つ。参照方向は
   Core ← Net ← Avatar ← Voice ← Inputs ← XR、UI は全部を参照、で循環なし。

   | アセンブリ | 参照 |
   |------------|------|
   | `Yaoron.Core` | (なし) |
   | `Yaoron.Net` | Core |
   | `Yaoron.Avatar` | Core, Net, `VRM10`, `UniGLTF`, `UniGLTF.Utils` |
   | `Yaoron.Voice` | Core, Avatar, `Normal.Realtime.Shared` |
   | `Yaoron.Inputs` | Core, Voice, `Unity.InputSystem` |
   | `Yaoron.XR` | Core, Inputs, Voice |
   | `Yaoron.UI` | Core, Net, Avatar, Voice, Inputs, `UnityEngine.UI` |

   注意点が 2 つある。**`autoReferenced` は定義済みアセンブリ (Assembly-CSharp) にしか効かない**ので、
   asmdef 側は `VRM10` や `Normal.Realtime.Shared` のような自動参照アセンブリも明示列挙が要る。
   一方 Normcore 本体 (`Normal.Realtime`) は precompiled DLL で Auto Reference が有効なため列挙不要。
   そもそも分割が必須になったのは、`IAwaitCaller` と `RuntimeOnlyNoThreadAwaitCaller` が入っている
   `UniGLTF.Utils` が **autoReferenced: false** で、asmdef からしか参照できないため。
   Editor スクリプトは asmdef を持たせず Assembly-CSharp-Editor のままにしてある。
   パッケージに依存する asmdef には `versionDefines` を入れてあるので、
   YA_NORMCORE / YA_VRM / YA_INPUTSYSTEM はエディタ拡張が走る前でも正しく立つ。

3. **UniTask は必須にしていない** (§2)。`Task.Delay` / `Task.Run` を使わず、
   待機はすべてコルーチン駆動の `YaAwait` に寄せてあるので WebGL でも安全。
   UniTask を入れた場合の置き換え先は `YaAwait` と `AvatarLoader` の 2 箇所だけ。

4. **入力は `.inputactions` を介さず直接ポーリング** (§10)。Input System 未導入でも
   動くようにするため。XR も XRI ではなく built-in の `InputDevices` から頭・両手の姿勢を取る。
   XRI 3.x を使う場合は `XRPlayerRig` を XR Origin 版に差し替えれば `IRigSource` はそのまま使える。

5. **腕の IK は Animation Rigging ではなく自前の 2 ボーン解析 IK** (§6)。
   ランタイムに読み込む VRM へリグ構造を後付けするコストを避けるため。

6. **入室は Normcore 3 の Quickmatch を既定にした** (§9 の代替)。
   `ConnectToNextAvailableQuickmatchRoom(グループ名, 定員 30)` を呼ぶと、サーバー側が
   空きのある部屋へ割り当てる。設計書が想定した「接続直後に人数を数えて満室なら繰り上げる」
   方式は、同時入室の競合で一瞬 30 人を超えうる不完全なものだったが、Quickmatch では
   定員がサーバーで守られる。`NormcoreConfig.useQuickmatch` を false にすると、
   従来の `{worldId}-{instance}` 連番 + `RoomCapacityGuard` の方式に戻る。
   ロビー (`LobbyDirectory`) は Quickmatch があれば MVP では不要なので既定で無効のまま。

7. **ロコモーションのアニメーションクリップは未同梱**。`AvatarPoseSolver` の
   `_locomotionController` に Humanoid 用コントローラ (パラメータ: `Speed` `MoveX` `MoveY`
   `Grounded` `Sit`) を割り当てると歩行・走行が入る。未設定の間は腕を下ろした簡易ポーズになる。

### バージョン差で踏んだ API の違い

設計書のコード片は Normcore 2.x / 古い UniVRM 前提だったため、以下は実 API に合わせてある。

| 箇所 | 設計書 | 実際 (導入版) |
|------|--------|---------------|
| `Realtime.Instantiate` | 名前付き bool 引数 | `Realtime.InstantiateOptions` を渡す。bool 版は `destroyWhenOwnerOrLastClientLeaves` に統合されている |
| ルーム時刻 | `realtime.room.time` | `realtime.roomTime` |
| `Vrm10.LoadBytesAsync` | `controlRigGeneration:` | `controlRigGenerationOption:` |
| タッチ入力 | 旧 `Input.touches` | Input System 導入時は `Touchscreen.current` を使う (Active Input Handling が New のみでも動く) |
| AwaitCaller | 素で参照できる想定 | `UniGLTF.Utils` が autoReferenced:false のため asmdef 必須 |

## 5. Normcore 3 で使える追加機能 (未使用)

導入は済んでいるが、まだ組み込んでいないもの。必要になった時点で差し替えられる。

- **AutoReconnect コンポーネント**: 指数バックオフ付きの自動再接続。`SessionController` の
  自前再接続 (3 回・固定間隔) を置き換えられる。サンプル UI も同梱されている。
- **EasySync / RealtimeAnimator**: コードを書かずにプロパティや Animator の状態を同期する。
  アバターのロコモーションを `AvatarStateModel.locomotion` ではなく Animator 同期にする選択肢。
- **`preferredRegions` / `Room.GetRegionsListAsync`**: 接続リージョンの明示指定 (設計書 §4 は自動任せ)。
- **`Room.GetConnectionStatistics`**: M0 で必要な帯域・遅延の実測に使える。

## 6. 残っている作業 (設計書 §14 の工程に対応)

- **M0**: Normcore の App Key 設定 → 各ターゲットでの接続確認 → **macOS Safari での音声送受信の実機検証** (最重要のリスク項目)。送信レートと帯域は `Room.GetConnectionStatistics` で実測し、設計書 §6 / §12 の仮置き値を差し替える。
- **M1**: プリセット VRM を用意して `AvatarCatalog` に登録。ロコモーション用の Animator コントローラ。
- **M2**: PC ↔ ブラウザ ↔ Android ↔ iOS の相互通話。エコー対策の実機確認。
- **M3**: Quest / PC VR での 3 点 IK とスナップターンの調整。
- **M4**: WebGL の HTTPS ホスティング手順、KTX2 テクスチャ。
- **M5**: Bot 29 台での 30 人負荷、LOD としきい値の再調整。
