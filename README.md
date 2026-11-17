# Lunar Trails

キーボード6キー（6K）とマウスを同時に使う、PC向けのリズムゲームです。

- 企画書（仮）・実装計画はチーム内で共有しているドキュメントを参照してください。
- 対応OS：Windows と Linux を先に進めます。macOS は Mac を持っているメンバーがいないため後回しです（Unity の設定やコードは macOS でも動く書き方にしておきます）。

## 開発環境

| 項目 | 内容 |
| --- | --- |
| Unity | **Unity 6 の最新 LTS（仮）**。チームで正式に決めたら、ここに番号を書いて全員そろえる。実際のバージョンは `ProjectSettings/ProjectVersion.txt` に記録される |
| テンプレート | Universal 3D（URP） |
| 入力 | Input System パッケージ（Universal 3D テンプレートに最初から入っている） |
| 音 | まず Unity 標準の AudioSource。CRI ADX LE は macOS / Linux 対応と利用条件を確認してから検討（差し替え方は下の「音の仕組みを差し替えるとき」） |
| 画像・音声 | Git LFS で管理する（`.gitattributes` に設定済み） |

### 最初のセットアップ

1. [Git](https://git-scm.com/) と Git LFS を入れる（Git for Windows には LFS が入っています）。一度だけ `git lfs install` を実行する。
2. このリポジトリを clone する。
3. Unity Hub の「追加」から clone したフォルダを開く。Hub が指定するバージョンの Unity を入れる。

> 最初に Unity でプロジェクトを開いた人は、Unity が作った `.meta` ファイルを必ずコミットしてください。`.meta` には各ファイルの ID が入っていて、人によって違う ID になるとシーンやプレハブの参照が切れます。

## フォルダ構成

自分たちで作るものはすべて `Assets/LunarTrails/` の下に置きます。Asset Store などから入れた素材と混ざらないようにするためです。

```
Assets/LunarTrails/
├── Scripts/
│   ├── Core/        … ゲームのルール（譜面・判定・時間の計算）。Unity に依存しない C#
│   │   ├── Timing/    拍と秒の変換（BPM 変化に対応）
│   │   ├── Charts/    譜面データの形と、読み込んだデータの検証・変換
│   │   └── Judging/   判定幅と、キーレーンの判定
│   └── Runtime/     … Unity の機能を使う部分
│       ├── Audio/     曲の再生と「曲の時計」
│       ├── Charts/    譜面 JSON の読み込み
│       ├── Input/     6K のキー入力（押した正確な時刻つき）
│       └── Gameplay/  試作用のゲーム進行
├── Tests/EditMode/  … Core の自動テスト
└── Charts/          … 譜面ファイル（JSON）
```

これから増える予定のフォルダ（中身ができたときに作る）：`Scenes/`、`Prefabs/`、`Art/`、`Audio/`、`UI/`。

### Core と Runtime を分けている理由

判定やスコアの計算は、画面表示や Unity の仕組みから切り離して `Core` に置いています（`LunarTrails.Core.asmdef` で Unity への参照を禁止しています）。

- 判定幅などの数値を調整したとき、自動テストですぐ確かめられる
- 表示（UI 案はまだ未決）を作り直しても、ルールの部分は書き直さなくてよい

`Runtime` は `Core` を使う側で、Unity の機能（音声、入力、画面）とつなぎます。

## 主な仕組み

### 時計：曲の再生位置がすべての基準

ゲーム中の「今の時刻」は、フレームの経過時間ではなく**曲の再生位置**から取ります（`ISongClock`）。フレームの時間で数えると、曲とノーツが少しずつずれていくためです。

### 入力：キーを押した正確な時刻を使う

`Update` でキーの状態を見ると、時刻がフレーム単位（60fps で最大約 17ms の誤差）になります。±40ms の判定には大きいので、`KeyLaneInputReader` は Input System の入力イベントに付いている時刻を記録します。キー配置の初期値は企画書どおり `S D F` と `J K L` です。

### 判定

判定幅は企画書の仮の値（PERFECT+ ±40ms、PERFECT ±60ms、HIT ±90ms）で、`Core/Judging/JudgeWindows.cs` にまとめています。今判定できるのは**キーレーンのタップとロングの始点だけ**です。マウスレーンのノーツ（クリック、トラック、フリック、トレイル）は、譜面の読み込みには対応していますが、判定は未実装です。

### 譜面

1 難易度につき 1 つの JSON ファイルで、ノーツの位置は拍で書きます。形式は [docs/chart-format.md](docs/chart-format.md) を参照してください。サンプル：`Assets/LunarTrails/Charts/sample_stl.json`。

### 音の仕組みを差し替えるとき

`Runtime/Audio/ISongClock.cs` を実装したクラスを新しく作り、使う側（今は `PrototypePlayer`）に渡す時計を入れ替えます。判定や譜面のコードは変更不要です。

## 試作シーンの組み立て方

ノーツの表示はまだありません（UI 案が決まってから作ります）。今は「曲を鳴らして、6K のタップ判定を Console に出す」ところまでです。

1. 新しいシーンを作り、空の GameObject を 1 つ置く。
2. そこに `AudioSource`、`AudioSourceSongClock`、`KeyLaneInputReader`、`PrototypePlayer` を付ける。
3. `AudioSource` の AudioClip に曲を入れ、Play On Awake を外す。
4. `PrototypePlayer` の欄に、譜面 JSON（例：`sample_stl.json`）と、同じ GameObject の `AudioSourceSongClock`・`KeyLaneInputReader` を入れる。
5. 再生して S D F J K L を叩くと、Console に `PerfectPlus (+12ms) #0 Tap ...` のように出ます。

サンプル譜面は BPM 120・オフセット 0 なので、テスト用には頭から一定の BPM 120 で始まる曲を使ってください。

## テストの実行

Unity の Window → General → Test Runner を開き、EditMode タブで Run All を押します。

## 次にやること（実装計画「曲を鳴らしてタップを流す」の残り）

- [ ] ノーツの表示（NoteView）。UI 案が決まってから
- [ ] 判定の表示
- [ ] オートプレイ（全ノーツを自動で取る。譜面と曲のずれ確認用）
- [ ] ロングの押し続け判定
- [ ] マウスレーンの入力と判定
