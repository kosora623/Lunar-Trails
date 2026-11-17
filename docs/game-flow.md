# ゲームの流れ（仮組み）

タイトル → 選曲 → プレイ → リザルト、と設定の画面。見た目は仮（OnGUI の文字だけ）で、
デザインが決まったら画面ごとに差し替える。

## シーンの組み立て

1. 新しいシーンを作り、空の GameObject に `GameDirector` を付ける。
   プレイに要る部品（`PrototypePlayer` など）は自動で付く。
2. Project ウィンドウで右クリック → Create → Lunar Trails → Song で、曲ごとにアセットを作る。
   - ID：記録の保存に使う。英数字で、あとから変えない（空ならアセット名）
   - 曲名・アーティスト・BPM の表記・ジャケット・音声・試聴の開始位置と長さ
   - Charts：譜面（.json か .sus）を難易度の数だけ。難易度とレベルは譜面の中のものを使い、stl → inv の順に並ぶ
3. Create → Lunar Trails → Song Library を作り、Songs に曲を並べる（この順で選曲画面に出る）。
4. `GameDirector` の Library にそれを入れる。

Library が空なら、`PrototypePlayer` の Songs を代わりに使う。

## 操作（仮の画面）

| 画面 | 操作 |
| --- | --- |
| タイトル | Enter：はじめる　Tab：設定　Esc：終わる |
| 選曲 | ↑↓：曲　←→：難易度　Enter：開始　A：オートプレイ　Tab：設定　Esc：タイトルへ |
| 設定 | ↑↓：項目　←→：値（Shift で大きく）　Enter：キーの割り当て　Esc / Tab：戻る |
| プレイ | S D F J K L とマウス。Esc：やめて選曲へ　R：やり直し　↑↓：オフセット |
| リザルト | Enter / Esc：選曲へ　R：もう一度 |

オートプレイの結果は記録しない。

## 保存するもの

`Application.persistentDataPath` に保存する。

- `settings.json`：設定
- `records.json`：譜面ごとの自己ベスト（最高達成率、最高ランプ、遊んだ回数）。キーは「曲の ID/難易度」

## 画面の差し替え

画面の流れは Core の `GameFlow` が決め、`GameDirector` が「今の画面の部品だけを有効にする」。

- `GameDirector` の Title Screen / Song Select Screen / Settings Screen / Result Screen の欄が空なら、
  仮の画面を自動で付ける。
- 本番の画面を作ったら、その部品を欄に入れる。
  同じ GameObject の部品なら enabled を、別の GameObject（Canvas など）なら GameObject ごと切り替える。
- 本番の画面は `GameScreenBase` を継承すると `Director` が使える。
  曲の一覧は `Director.Songs`、自己ベストは `Director.FindRecord`、直前の結果は `Director.Last`。
  画面を移るときは `StartSong` / `Retry` / `GoToSongSelect` / `OpenSettings` / `Back` を呼ぶ。
- 画面が切り替わったフレームは `Director.AcceptsInput` が false になる。
  前の画面で押した Enter を新しい画面が拾わないように、操作を読む前に確かめる（`ReadableKeyboard()` がこれをする）。

プレイ画面は今のところ `PrototypePlayer`（立方体と文字）のまま。
