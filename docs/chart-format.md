# 譜面ファイルの形式（version 1・試作用）

譜面は 1 難易度につき 1 つの JSON ファイルです。ノーツの位置は秒ではなく**拍**で書きます。BPM を直しても譜面がずれず、エディタでも扱いやすいためです。

この形式は試作用の案で、試遊のあとに見直します。読み込み処理は `Assets/LunarTrails/Scripts/Core/Charts/` にあります。

## 例

```json
{
  "version": 1,
  "song": "test01",
  "difficulty": "lnr",
  "level": 12.5,
  "offsetMs": 0,
  "bpm": [{ "beat": 0, "value": 180 }],
  "notes": [
    { "type": "tap",   "beat": 4.0,  "lane": 0, "width": 1 },
    { "type": "long",  "beat": 5.0,  "lane": 1, "width": 1, "length": 2.0 },
    { "type": "guide", "beat": 6.0,  "lane": 4, "width": 1 },
    { "type": "click", "beat": 8.0 },
    { "type": "track", "beat": 8.5,  "x": 0.2, "size": 0.2 },
    { "type": "flick", "beat": 10.0, "x": 0.7, "size": 0.5 },
    { "type": "trail", "beat": 12.0, "size": 0.2, "endFlick": false,
      "points": [
        { "beat": 0.0, "x": 0.3 },
        { "beat": 2.0, "x": 0.7, "curve": "smooth" },
        { "beat": 4.0, "x": 0.4, "curve": "smooth" }
      ] }
  ]
}
```

## 全体の項目

| 項目 | 意味 |
| --- | --- |
| `version` | 形式のバージョン。今は `1` |
| `song` | 曲の ID |
| `difficulty` | 難易度の略称（`stl` / `ast` / `lnr` / `nov` / `inv`） |
| `level` | レベル（例：`12.5`） |
| `offsetMs` | 音声ファイルの頭から 1 拍目までの時間（ミリ秒） |
| `bpm` | BPM の変化点。`beat` 0 の指定は必須 |
| `notes` | ノーツの一覧。順番は自由（読み込み時に時刻順に並べ替える） |

## ノーツの項目

| 項目 | 使うノーツ | 意味 |
| --- | --- | --- |
| `type` | すべて | `tap` / `long` / `guide` / `click` / `track` / `flick` / `trail` |
| `beat` | すべて | 曲の頭から数えた拍の位置 |
| `lane` | tap, long, guide | キーレーンの番号（左から 0〜5） |
| `width` | tap, long, guide | 右へ何レーン分の幅か。省略すると 1 |
| `length` | long | ロングの長さ（拍） |
| `x` | track, flick | マウスレーン内の横位置。左端が 0、右端が 1 |
| `size` | track, flick, trail | マウスレーン内の幅（0〜1） |
| `points` | trail | 通過点。`beat` はトレイルの始点からの拍。2 つ以上必要 |
| `curve` | trail の points | 前の点からのつなぎ方。`linear`（直線、省略時）か `smooth`（なめらか） |
| `endFlick` | trail | 終点にフリックを置くか |

## 読み込み時にエラーになるもの

- `version` が 1 以外
- `bpm` がない、`beat` 0 の BPM がない、BPM が 0 以下
- 知らない `type` や `curve`
- キーレーンのノーツが 0〜5 からはみ出す（例：`lane` 5 で `width` 2）
- ロングの `length` が 0 以下
- トレイルの `points` が 2 つ未満

エラーのメッセージは「notes[3]: ...」のように、何番目のノーツかが分かる形で出ます。
