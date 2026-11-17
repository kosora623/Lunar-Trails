using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LunarTrails.Core.Charts;
using LunarTrails.Core.Charts.Sus;
using LunarTrails.Core.Editing;
using LunarTrails.Core.Timing;
using LunarTrails.Runtime.Audio;
using LunarTrails.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.Networking;

namespace LunarTrails.Runtime.ChartEditing
{
    /// <summary>
    /// 試作用の譜面エディタ。見た目は仮（OnGUI の四角と文字だけ）。
    /// ゲームと同じ並び（左 3 キー・マウスレーン・右 3 キー）を上から見た図で、下から上へ時間が進む。
    /// </summary>
    /// <remarks>
    /// 使い方：
    /// 1. 新しいシーンに空の GameObject を置き、ChartEditor を付ける（試しプレイ用の PrototypePlayer も自動で付く）。
    /// 2. 再生すると編集画面が出る。操作は画面左の説明を参照。
    /// 3. 単体アプリとして書き出すときは、このシーンだけを Build Profiles に入れて書き出す。
    ///
    /// 保存形式は Lunar Trails の JSON（docs/chart-format.md）。.sus も読み込める（保存は JSON）。
    /// 編集の中身（元に戻す、拍への吸着、ノーツの当たり判定など）は Core/Editing にあり、テストもそちらにある。
    /// </remarks>
    [RequireComponent(typeof(PrototypePlayer))]
    public sealed class ChartEditor : MonoBehaviour
    {
        private enum Tool { Tap, Long, Click, Track, Flick, Trail, Guide }

        private static readonly string[] ToolNames = { "タップ", "ロング", "クリック", "トラック", "フリック", "トレイル", "ガイド" };

        [Tooltip("最初に開く譜面（空なら新しい譜面）")]
        [SerializeField] private TextAsset _openOnStart;

        [Tooltip("曲の音声（空なら、画面の「音声ファイル」から読み込む）")]
        [SerializeField] private AudioClip _audio;

        // ===== 画面の配置（ピクセル） =====
        private const float PanelWidth = 300f;
        private const float KeyColumnWidth = 48f;
        private const float MouseColumnWidth = KeyColumnWidth * 4f;
        private const float BottomMargin = 60f;
        private const float NoteHeight = 8f;
        private const int TrailDrawSteps = 8;

        private PrototypePlayer _player;
        private HitSoundPlayer _hitSound;
        private AudioSource _preview;

        private ChartDocument _doc = new ChartDocument();
        private TempoMap _tempo;

        // 表示
        private double _viewBeat;          // 判定ライン（画面の下の線）の位置の拍
        private float _pixelsPerBeat = 80f;
        private int _divisionIndex = 3;    // EditorGrid.Divisions の何番目か（初期は 1 拍 4 等分）

        // 置くノーツ
        private Tool _tool = Tool.Tap;
        private int _newWidth = 1;
        private float _newSize = 0.25f;

        // マウス操作の途中の状態
        private int _dragIndex = -1;            // 動かしているノーツ
        private float _dragGrabX;
        private bool _dragMoved;
        private bool _longPlacing;              // ロングを置いている途中（押した所が始点、離した所が終点）
        private double _longStartBeat;
        private int _longLane;
        private NoteData _buildingTrail;        // 通過点を足している途中のトレイル

        // 再生（試し聞き）
        private bool _previewing;
        private double _previewLastBeat;
        private double _previewStartRealtime;
        private double _previewStartSeconds;

        // 左の欄の入力
        private string _filePath = "";
        private string _audioPath = "";
        private string _songText, _difficultyText, _levelText, _offsetText, _bpmText;
        private string _message = "";
        private Vector2 _panelScroll;

        private GUIStyle _smallLabel;
        private GUIStyle _richLabel;
        private GUIStyle _titleLabel;
        private GUIStyle _messageLabel;
        private GUIStyle _helpLabel;

        private int DivisionsPerBeat => EditorGrid.Divisions[_divisionIndex];

        // ===== 準備 =====

        private void Awake()
        {
            _player = GetComponent<PrototypePlayer>();
            _hitSound = GetComponent<HitSoundPlayer>();
            if (_hitSound == null) _hitSound = gameObject.AddComponent<HitSoundPlayer>();
            var previewObject = new GameObject("EditorPreviewAudio");
            previewObject.transform.SetParent(transform, false);
            _preview = previewObject.AddComponent<AudioSource>();
            _preview.playOnAwake = false;

            string folder = Application.isEditor ? Path.Combine(Application.dataPath, "LunarTrails", "Charts") : Application.persistentDataPath;
            _filePath = Path.Combine(folder, "new_chart.json");
        }

        private void Start()
        {
            // 試しプレイ用の PrototypePlayer は使うが、その選曲画面は使わない
            var menu = GetComponent<PrototypeMenu>();
            if (menu != null) menu.enabled = false;

            if (_openOnStart != null) OpenText(_openOnStart.text, _openOnStart.name);
            else SetDocument(new ChartDocument());
        }

        private void SetDocument(ChartDocument doc)
        {
            _doc = doc;
            _buildingTrail = null;
            _viewBeat = 0;
            RefreshHeaderTexts();
            RebuildTempo();
        }

        private void RefreshHeaderTexts()
        {
            var d = _doc.Data;
            _songText = d.song ?? "";
            _difficultyText = d.difficulty ?? "";
            _levelText = d.level.ToString(CultureInfo.InvariantCulture);
            _offsetText = d.offsetMs.ToString(CultureInfo.InvariantCulture);
            _bpmText = string.Join(", ", d.bpm.Select(b => $"{b.beat.ToString(CultureInfo.InvariantCulture)}:{b.value.ToString(CultureInfo.InvariantCulture)}"));
        }

        private void RebuildTempo()
        {
            try
            {
                _tempo = new TempoMap(_doc.Data.bpm.Select(b => (b.beat, b.value)));
            }
            catch (ArgumentException e)
            {
                _message = "BPM が正しくありません: " + e.Message;
                _tempo = new TempoMap(new[] { (0.0, 120.0) });
            }
        }

        private double OffsetSeconds => _doc.Data.offsetMs / 1000.0;
        private double BeatToSongSeconds(double beat) => OffsetSeconds + _tempo.BeatToSeconds(beat);
        private double SongSecondsToBeat(double seconds) => _tempo.SecondsToBeat(seconds - OffsetSeconds);

        // ===== 毎フレーム =====

        private void Update()
        {
            if (_player.IsPlaying) return;
            if (_previewing) UpdatePreview();
        }

        private void UpdatePreview()
        {
            double seconds = _preview.clip != null && _preview.isPlaying
                ? _preview.time
                : _previewStartSeconds + (Time.realtimeSinceStartupAsDouble - _previewStartRealtime);
            double beat = SongSecondsToBeat(seconds);

            // 通り過ぎたノーツで叩く音を鳴らす
            foreach (var n in _doc.Notes)
            {
                if (n.type == "track" || n.type == "guide") continue;
                if (n.beat > _previewLastBeat && n.beat <= beat) _hitSound.PlayNow();
            }
            _previewLastBeat = beat;
            _viewBeat = beat;

            if (_preview.clip != null && !_preview.isPlaying) StopPreview(); // 曲が終わった
        }

        private void StartPreview()
        {
            CancelPlacing();
            _previewing = true;
            _previewLastBeat = _viewBeat;
            _previewStartSeconds = BeatToSongSeconds(_viewBeat);
            _previewStartRealtime = Time.realtimeSinceStartupAsDouble;
            var clip = CurrentClip;
            if (clip != null && _previewStartSeconds >= 0 && _previewStartSeconds < clip.length)
            {
                _preview.clip = clip;
                _preview.time = (float)_previewStartSeconds;
                _preview.Play();
            }
            else
            {
                _preview.clip = null; // 音声がないか範囲外なら、時計だけで進める
            }
        }

        private void StopPreview()
        {
            _previewing = false;
            _preview.Stop();
            _viewBeat = EditorGrid.SnapBeat(_viewBeat, DivisionsPerBeat);
        }

        private AudioClip CurrentClip => _audio;

        // ===== 描画と操作 =====

        private void OnGUI()
        {
            if (_player.IsPlaying) return;
            if (_smallLabel == null)
            {
                _smallLabel = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _richLabel = new GUIStyle(GUI.skin.label) { richText = true };
                _titleLabel = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 };
                _messageLabel = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = new Color(1f, 0.85f, 0.5f) } };
                _helpLabel = new GUIStyle(_smallLabel) { richText = true, wordWrap = true };
            }

            // 試しプレイの 3D 表示を隠すため、まず全体を塗る
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.08f, 0.1f));

            DrawPanel();
            HandleKeys(Event.current);
            HandleMouse(Event.current);
            if (Event.current.type == EventType.Repaint) DrawChart();
        }

        // ---- 画面の座標 ----

        private float ChartLeft => PanelWidth + Mathf.Max(20f, (Screen.width - PanelWidth - TotalWidth) / 2f);
        private static float TotalWidth => KeyColumnWidth * 6 + MouseColumnWidth;
        private float LineY => Screen.height - BottomMargin;

        private float KeyColumnX(int lane) => ChartLeft + (lane < 3 ? lane * KeyColumnWidth : 3 * KeyColumnWidth + MouseColumnWidth + (lane - 3) * KeyColumnWidth);
        private float MouseLeft => ChartLeft + 3 * KeyColumnWidth;
        private float MouseX(float x) => MouseLeft + MouseColumnWidth * x;
        private float BeatToY(double beat) => LineY - (float)((beat - _viewBeat) * _pixelsPerBeat);
        private double YToBeat(float y) => _viewBeat + (LineY - y) / _pixelsPerBeat;

        /// <summary>画面の X から列を求める。譜面の外なら false。</summary>
        private bool TryColumnAt(float x, out NotePicker.Column column)
        {
            column = default;
            if (x < ChartLeft || x >= ChartLeft + TotalWidth) return false;
            if (x >= MouseLeft && x < MouseLeft + MouseColumnWidth)
            {
                column = NotePicker.Column.Mouse((x - MouseLeft) / MouseColumnWidth);
                return true;
            }
            int lane = x < MouseLeft
                ? (int)((x - ChartLeft) / KeyColumnWidth)
                : 3 + (int)((x - MouseLeft - MouseColumnWidth) / KeyColumnWidth);
            column = NotePicker.Column.Key(Mathf.Clamp(lane, 0, 5));
            return true;
        }

        private bool IsToolForMouseLane(Tool tool) => tool == Tool.Click || tool == Tool.Track || tool == Tool.Flick || tool == Tool.Trail;

        // ---- キー操作 ----

        private void HandleKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if (GUIUtility.keyboardControl != 0) // 左の欄で文字を入力中
            {
                if (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return)
                {
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                }
                return;
            }

            bool ctrl = e.control || e.command;
            switch (e.keyCode)
            {
                case KeyCode.Alpha1: SelectTool(Tool.Tap); break;
                case KeyCode.Alpha2: SelectTool(Tool.Long); break;
                case KeyCode.Alpha3: SelectTool(Tool.Click); break;
                case KeyCode.Alpha4: SelectTool(Tool.Track); break;
                case KeyCode.Alpha5: SelectTool(Tool.Flick); break;
                case KeyCode.Alpha6: SelectTool(Tool.Trail); break;
                case KeyCode.Alpha7: SelectTool(Tool.Guide); break;
                case KeyCode.Q: _divisionIndex = Mathf.Max(0, _divisionIndex - 1); break;
                case KeyCode.E: _divisionIndex = Mathf.Min(EditorGrid.Divisions.Length - 1, _divisionIndex + 1); break;
                case KeyCode.LeftBracket: ChangeNewSize(-1); break;
                case KeyCode.RightBracket: ChangeNewSize(+1); break;
                case KeyCode.Space:
                    if (_previewing) StopPreview(); else StartPreview();
                    break;
                case KeyCode.Home: _viewBeat = 0; break;
                case KeyCode.UpArrow: _viewBeat += 1.0 / DivisionsPerBeat; break;
                case KeyCode.DownArrow: _viewBeat = Math.Max(0, _viewBeat - 1.0 / DivisionsPerBeat); break;
                case KeyCode.PageUp: _viewBeat += 4; break;
                case KeyCode.PageDown: _viewBeat = Math.Max(0, _viewBeat - 4); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    FinishTrail();
                    break;
                case KeyCode.Escape: CancelPlacing(); break;
                case KeyCode.F: ToggleEndFlick(); break;
                case KeyCode.Z when ctrl: if (e.shift) Redo(); else Undo(); break;
                case KeyCode.Y when ctrl: Redo(); break;
                case KeyCode.S when ctrl: Save(); break;
                case KeyCode.P: TestPlay(); break;
                default: return;
            }
            e.Use();
        }

        private void SelectTool(Tool tool)
        {
            FinishTrail();
            _tool = tool;
        }

        // [ ] で、これから置くノーツの幅を変える（マウスの下にノーツがあれば、そのノーツの幅を変える）
        private void ChangeNewSize(int step)
        {
            int hovered = HoveredNote();
            if (hovered >= 0)
            {
                _doc.ChangeNote(hovered, n => NoteEditing.Resize(n, step));
                return;
            }
            if (IsToolForMouseLane(_tool))
                _newSize = Mathf.Clamp(_newSize + step / (float)EditorGrid.MouseColumns, 1f / EditorGrid.MouseColumns, 1f);
            else
                _newWidth = Mathf.Clamp(_newWidth + step, 1, 6);
        }

        private int HoveredNote()
        {
            var mouse = Event.current.mousePosition;
            if (!TryColumnAt(mouse.x, out var column)) return -1;
            return NotePicker.Find(_doc, YToBeat(mouse.y), column, PickTolerance);
        }

        private double PickTolerance => 10.0 / _pixelsPerBeat; // 頭の上下 10 ピクセル

        private void ToggleEndFlick()
        {
            int index = _buildingTrail != null ? IndexOf(_buildingTrail) : HoveredNote();
            if (index < 0 || _doc.Notes[index].type != "trail") return;
            _doc.ChangeNote(index, n => n.endFlick = !n.endFlick);
        }

        private void Undo()
        {
            ResetPlacing();
            _doc.Undo();
            RefreshHeaderTexts();
            RebuildTempo();
        }

        private void Redo()
        {
            ResetPlacing();
            _doc.Redo();
            RefreshHeaderTexts();
            RebuildTempo();
        }

        // ---- マウス操作 ----

        private void HandleMouse(Event e)
        {
            if (e.type == EventType.ScrollWheel && e.mousePosition.x > PanelWidth)
            {
                if (e.control || e.command)
                    _pixelsPerBeat = Mathf.Clamp(_pixelsPerBeat * (e.delta.y > 0 ? 0.9f : 1.1f), 20f, 400f);
                else if (!_previewing)
                    _viewBeat = Math.Max(0, _viewBeat - Math.Sign(e.delta.y) * 1.0 / DivisionsPerBeat * (e.shift ? 4 : 1));
                e.Use();
                return;
            }

            if (_previewing) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag && e.type != EventType.MouseUp) return;
            if (!TryColumnAt(e.mousePosition.x, out var column))
            {
                if (e.type == EventType.MouseUp)
                {
                    _longPlacing = false; // 譜面の外で離したら取り消し
                    EndDrag(null, 0);
                }
                return;
            }
            if (e.type == EventType.MouseDown) GUIUtility.keyboardControl = 0; // 左の欄の入力から抜ける
            double beat = Math.Max(0, EditorGrid.SnapBeat(YToBeat(e.mousePosition.y), DivisionsPerBeat));

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                // 右クリック：消す
                int hit = NotePicker.Find(_doc, YToBeat(e.mousePosition.y), column, PickTolerance);
                if (hit >= 0)
                {
                    if (_doc.Notes[hit] == _buildingTrail) _buildingTrail = null;
                    _doc.RemoveNote(hit);
                }
                e.Use();
                return;
            }
            if (e.button != 0) return;

            switch (e.type)
            {
                case EventType.MouseDown:
                    OnLeftDown(beat, column);
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (_dragIndex >= 0) _dragMoved = true;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (_longPlacing) FinishLong(beat);
                    else EndDrag(column, beat);
                    e.Use();
                    break;
            }
        }

        private void OnLeftDown(double beat, NotePicker.Column column)
        {
            // トレイルを作っている途中なら、クリックで通過点を足す（Shift を押しながらだとなめらかにつなぐ）
            if (_buildingTrail != null)
            {
                int index = IndexOf(_buildingTrail);
                if (index < 0) { _buildingTrail = null; }
                else if (column.IsMouse)
                {
                    double rel = beat - _buildingTrail.beat;
                    if (rel > 0)
                    {
                        bool smooth = Event.current.shift;
                        _doc.ChangeNote(index, n => NoteEditing.AddTrailPoint(n, rel, column.MouseX, smooth));
                    }
                    return;
                }
            }

            // ノーツの上なら、つかんで動かす
            int hit = NotePicker.Find(_doc, YToBeat(Event.current.mousePosition.y), column, PickTolerance);
            if (hit >= 0)
            {
                _dragIndex = hit;
                _dragMoved = false;
                _dragGrabX = column.IsMouse ? column.MouseX - NoteEditing.HeadX(_doc.Notes[hit]) : 0f;
                return;
            }

            if (IsToolForMouseLane(_tool) != column.IsMouse)
            {
                _message = IsToolForMouseLane(_tool) ? $"{ToolNames[(int)_tool]}はマウスレーン（真ん中）に置きます。" : $"{ToolNames[(int)_tool]}はキーレーンに置きます。";
                return;
            }
            PlaceNew(beat, column);
        }

        private void PlaceNew(double beat, NotePicker.Column column)
        {
            int lane = Mathf.Clamp(column.KeyLane, 0, 6 - _newWidth);
            float x = EditorGrid.SnapCenter(column.MouseX, _newSize);
            switch (_tool)
            {
                case Tool.Tap:
                    _doc.AddNote(new NoteData { type = "tap", beat = beat, lane = lane, width = _newWidth });
                    break;
                case Tool.Guide:
                    _doc.AddNote(new NoteData { type = "guide", beat = beat, lane = lane, width = _newWidth });
                    break;
                case Tool.Long:
                    _longPlacing = true;
                    _longStartBeat = beat;
                    _longLane = lane;
                    break;
                case Tool.Click:
                    _doc.AddNote(new NoteData { type = "click", beat = beat });
                    break;
                case Tool.Track:
                    _doc.AddNote(new NoteData { type = "track", beat = beat, x = x, size = _newSize });
                    break;
                case Tool.Flick:
                    _doc.AddNote(new NoteData { type = "flick", beat = beat, x = x, size = _newSize });
                    break;
                case Tool.Trail:
                    var trail = new NoteData
                    {
                        type = "trail", beat = beat, size = _newSize,
                        points = new[] { new TrailPointData { beat = 0, x = EditorGrid.SnapX(column.MouseX), curve = "linear" } },
                    };
                    _doc.AddNote(trail);
                    _buildingTrail = trail;
                    _message = "クリックで通過点を足す（Shift でなめらか）。Enter で完成、F で終点フリック。";
                    break;
            }
        }

        private void FinishLong(double endBeat)
        {
            _longPlacing = false;
            double length = Math.Max(1.0 / DivisionsPerBeat, endBeat - _longStartBeat);
            _doc.AddNote(new NoteData { type = "long", beat = _longStartBeat, lane = _longLane, width = _newWidth, length = length });
        }

        private void EndDrag(NotePicker.Column? column, double beat)
        {
            int index = _dragIndex;
            _dragIndex = -1;
            if (index < 0 || !_dragMoved || !column.HasValue || index >= _doc.Notes.Count) return;
            var col = column.Value;
            float grab = _dragGrabX;
            var copy = ChartDataCopy.Clone(_doc.Notes[index]);
            if (!NoteEditing.MoveTo(copy, beat, col, grab)) return; // 種類に合わない列には動かさない
            _doc.ChangeNote(index, n => NoteEditing.MoveTo(n, beat, col, grab));
        }

        private void FinishTrail()
        {
            if (_buildingTrail == null) return;
            int index = IndexOf(_buildingTrail);
            if (index >= 0 && (_buildingTrail.points == null || _buildingTrail.points.Length < 2))
            {
                _doc.RemoveNote(index); // 通過点が 1 つだけのトレイルは作らない
                _message = "トレイルには通過点が 2 つ以上必要なので、取り消しました。";
            }
            _buildingTrail = null;
        }

        private void CancelPlacing()
        {
            _longPlacing = false;
            _dragIndex = -1;
            FinishTrail();
        }

        // 置いている途中の状態だけを捨てる（元に戻すとき用。譜面は変えない）
        private void ResetPlacing()
        {
            _longPlacing = false;
            _dragIndex = -1;
            _buildingTrail = null;
        }

        private int IndexOf(NoteData note)
        {
            var notes = _doc.Data.notes;
            for (int i = 0; i < notes.Length; i++)
                if (ReferenceEquals(notes[i], note)) return i;
            return -1;
        }

        // ---- 描画 ----

        private void DrawChart()
        {
            float top = 0f;
            float height = Screen.height;

            // レーン
            for (int lane = 0; lane < 6; lane++)
                Fill(new Rect(KeyColumnX(lane) + 1, top, KeyColumnWidth - 2, height), new Color(0.15f, 0.15f, 0.18f));
            Fill(new Rect(MouseLeft + 1, top, MouseColumnWidth - 2, height), new Color(0.11f, 0.11f, 0.14f));
            for (int i = 1; i < EditorGrid.MouseColumns; i++) // マウスレーンの 1/16 の目盛り
                Fill(new Rect(MouseX(i / (float)EditorGrid.MouseColumns), top, 1, height), new Color(1, 1, 1, i % 4 == 0 ? 0.08f : 0.03f));

            // 拍の線（4 拍ごとに太く、小節の番号を出す）
            double firstBeat = Math.Max(0, EditorGrid.FloorBeat(YToBeat(height), DivisionsPerBeat));
            double lastBeat = YToBeat(0);
            double step = 1.0 / DivisionsPerBeat;
            for (double b = firstBeat; b <= lastBeat; b += step)
            {
                double rounded = Math.Round(b * DivisionsPerBeat) / DivisionsPerBeat;
                float y = BeatToY(rounded);
                bool isBeat = Math.Abs(rounded - Math.Round(rounded)) < 1e-6;
                bool isMeasure = isBeat && ((int)Math.Round(rounded)) % 4 == 0;
                var color = isMeasure ? new Color(1, 1, 1, 0.5f) : isBeat ? new Color(1, 1, 1, 0.22f) : new Color(1, 1, 1, 0.07f);
                Fill(new Rect(ChartLeft, y, TotalWidth, isMeasure ? 2 : 1), color);
                if (isMeasure)
                    GUI.Label(new Rect(ChartLeft - 64, y - 9, 60, 18), $"{(int)Math.Round(rounded) / 4 + 1} 小節", _smallLabel);
            }

            // ノーツ（ロングとトレイルの帯を先に描き、頭を上に重ねる）
            var notes = _doc.Notes;
            foreach (var n in notes) DrawBody(n);
            foreach (var n in notes) DrawHead(n);

            // つかんでいるノーツ・置く場所の目安
            DrawCursorGuide();

            // 判定ライン（今の位置）
            Fill(new Rect(ChartLeft - 8, LineY - 1, TotalWidth + 16, 3), _previewing ? new Color(1f, 0.4f, 0.4f) : Color.white);
        }

        private void DrawBody(NoteData n)
        {
            if (n.type == "long")
            {
                float y0 = BeatToY(n.beat), y1 = BeatToY(n.beat + n.length);
                var rect = KeyRect(n.lane, n.width, y1, y0 - y1);
                rect.x += rect.width * 0.15f;
                rect.width *= 0.7f;
                Fill(rect, new Color(0.1f, 0.25f, 0.55f));
            }
            else if (n.type == "trail" && n.points != null && n.points.Length >= 1)
            {
                var color = ReferenceEquals(n, _buildingTrail) ? new Color(1f, 0.75f, 0.3f, 0.9f) : new Color(1f, 0.55f, 0.1f, 0.85f);
                float width = Mathf.Max(4f, n.size * MouseColumnWidth);
                var points = TrailPointsInBeats(n);
                if (points.Count >= 2)
                {
                    double start = points[0].Time, end = points[points.Count - 1].Time;
                    int steps = TrailDrawSteps * (points.Count - 1);
                    var prev = new Vector2(MouseX(TrailPath.XAt(points, start)), BeatToY(n.beat + start));
                    for (int i = 1; i <= steps; i++)
                    {
                        double rel = start + (end - start) * i / steps;
                        var p = new Vector2(MouseX(TrailPath.XAt(points, rel)), BeatToY(n.beat + rel));
                        DrawLine(prev, p, width, color);
                        prev = p;
                    }
                }
                // 通過点（なめらかにつなぐ点は丸っぽく見えるよう小さめ）
                foreach (var p in n.points)
                {
                    float size = p.curve == "smooth" ? 6f : 8f;
                    Fill(new Rect(MouseX(p.x) - size / 2, BeatToY(n.beat + p.beat) - size / 2, size, size), Color.white);
                }
                if (n.endFlick)
                {
                    var last = n.points[n.points.Length - 1];
                    DrawMouseNote(last.x, n.size, BeatToY(n.beat + last.beat), new Color(1f, 0.2f, 0.2f), "フリック");
                }
            }
        }

        private void DrawHead(NoteData n)
        {
            float y = BeatToY(n.beat);
            if (y < -20 || y > Screen.height + 20) return;
            switch (n.type)
            {
                case "tap":
                case "long":
                    Fill(KeyRect(n.lane, n.width, y - NoteHeight / 2, NoteHeight), new Color(0.2f, 0.5f, 1f));
                    break;
                case "guide":
                    Fill(KeyRect(n.lane, n.width, y - 2, 4), Color.white);
                    break;
                case "click":
                    Fill(new Rect(MouseLeft + 2, y - NoteHeight / 2, MouseColumnWidth - 4, NoteHeight), new Color(0.3f, 0.8f, 1f));
                    GUI.Label(new Rect(MouseLeft + 4, y - 20, 80, 16), "クリック", _smallLabel);
                    break;
                case "track":
                    DrawMouseNote(n.x, n.size, y, new Color(1f, 0.85f, 0.1f), null);
                    break;
                case "flick":
                    DrawMouseNote(n.x, n.size, y, new Color(1f, 0.2f, 0.2f), "フリック");
                    break;
            }
        }

        private void DrawMouseNote(float x, float size, float y, Color color, string label)
        {
            float width = Mathf.Max(6f, size * MouseColumnWidth);
            Fill(new Rect(MouseX(x) - width / 2, y - NoteHeight / 2, width, NoteHeight), color);
            if (label != null) GUI.Label(new Rect(MouseX(x) - width / 2, y - 20, 80, 16), label, _smallLabel);
        }

        private void DrawCursorGuide()
        {
            var mouse = Event.current.mousePosition;
            if (!TryColumnAt(mouse.x, out var column) || _previewing) return;
            double beat = Math.Max(0, EditorGrid.SnapBeat(YToBeat(mouse.y), DivisionsPerBeat));
            float y = BeatToY(beat);
            var ghost = new Color(1, 1, 1, 0.25f);

            if (_longPlacing)
            {
                float y0 = BeatToY(_longStartBeat);
                Fill(KeyRect(_longLane, _newWidth, Mathf.Min(y, y0), Mathf.Abs(y0 - y) + NoteHeight / 2), ghost);
                return;
            }
            if (_dragIndex >= 0 && _dragMoved && _dragIndex < _doc.Notes.Count)
            {
                var copy = ChartDataCopy.Clone(_doc.Notes[_dragIndex]);
                if (NoteEditing.MoveTo(copy, beat, column, _dragGrabX))
                {
                    if (copy.type == "tap" || copy.type == "long" || copy.type == "guide")
                        Fill(KeyRect(copy.lane, copy.width, y - NoteHeight / 2, NoteHeight), ghost);
                    else if (copy.type == "click")
                        Fill(new Rect(MouseLeft, y - NoteHeight / 2, MouseColumnWidth, NoteHeight), ghost);
                    else
                        DrawMouseNote(NoteEditing.HeadX(copy), Mathf.Max(copy.size, 1f / EditorGrid.MouseColumns), y, ghost, null);
                }
                return;
            }
            if (IsToolForMouseLane(_tool) != column.IsMouse) return;

            if (column.IsMouse)
            {
                if (_tool == Tool.Click) Fill(new Rect(MouseLeft, y - NoteHeight / 2, MouseColumnWidth, NoteHeight), ghost);
                else if (_tool == Tool.Trail && _buildingTrail != null)
                    Fill(new Rect(MouseX(EditorGrid.SnapX(column.MouseX)) - 4, y - 4, 8, 8), ghost);
                else DrawMouseNote(EditorGrid.SnapCenter(column.MouseX, _newSize), _newSize, y, ghost, null);
            }
            else
            {
                Fill(KeyRect(Mathf.Clamp(column.KeyLane, 0, 6 - _newWidth), _newWidth, y - NoteHeight / 2, NoteHeight), ghost);
            }
        }

        private Rect KeyRect(int lane, int width, float y, float height)
        {
            width = Mathf.Max(1, width);
            int last = Mathf.Min(5, lane + width - 1);
            float left = KeyColumnX(lane);
            float right = KeyColumnX(last) + KeyColumnWidth; // 左右のキーにまたがるときは、マウスレーンの上も通る
            return new Rect(left + 3, y, right - left - 6, height);
        }

        // トレイルの通過点を「時刻 = 始点からの拍」として TrailPoint にする（ゲームと同じ曲線の計算を使うため）
        private static List<TrailPoint> TrailPointsInBeats(NoteData n)
        {
            var list = new List<TrailPoint>();
            foreach (var p in n.points)
                list.Add(new TrailPoint(p.beat, p.x, p.curve == "smooth" ? CurveType.Smooth : CurveType.Linear));
            return list;
        }

        // ---- 左の欄 ----

        private void DrawPanel()
        {
            GUILayout.BeginArea(new Rect(8, 8, PanelWidth - 16, Screen.height - 16));
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            GUILayout.Label("<b>譜面エディタ（試作）</b>" + (_doc.IsDirty ? "　＊未保存" : ""), _titleLabel);

            GUILayout.Space(4);
            GUILayout.Label($"置くノーツ：<b>{ToolNames[(int)_tool]}</b>", _richLabel);
            int tool = GUILayout.SelectionGrid((int)_tool, ToolNames, 4);
            if (tool != (int)_tool) SelectTool((Tool)tool);
            GUILayout.Label(IsToolForMouseLane(_tool)
                ? $"幅 {_newSize * EditorGrid.MouseColumns:0}/16（[ ] で変更）"
                : $"幅 {_newWidth} レーン（[ ] で変更）");
            GUILayout.Label($"拍の細かさ：1 拍を {DivisionsPerBeat} 等分（Q / E）");
            GUILayout.Label($"今の位置：{_viewBeat / 4 + 1:0.00} 小節（{_viewBeat:0.##} 拍、{BeatToSongSeconds(_viewBeat):0.000} 秒）");
            GUILayout.Label($"ノーツ {_doc.Notes.Count} 個");

            GUILayout.Space(8);
            GUILayout.Label("<b>曲の情報</b>（入力したら「反映」）", _richLabel);
            _songText = LabeledField("曲 ID", _songText);
            _difficultyText = LabeledField("難易度", _difficultyText);
            _levelText = LabeledField("レベル", _levelText);
            _offsetText = LabeledField("オフセット ms", _offsetText);
            _bpmText = LabeledField("BPM（拍:値, …）", _bpmText);
            if (GUILayout.Button("反映")) ApplyHeader();

            GUILayout.Space(8);
            GUILayout.Label("<b>ファイル</b>", _richLabel);
            GUILayout.Label("譜面（.json / .sus）", _smallLabel);
            _filePath = GUILayout.TextField(_filePath);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("開く")) OpenFile(_filePath);
            if (GUILayout.Button("保存（Ctrl+S）")) Save();
            if (GUILayout.Button("新規")) { SetDocument(new ChartDocument()); _message = "新しい譜面を作りました。"; }
            GUILayout.EndHorizontal();
            GUILayout.Label("音声（.wav / .ogg / .mp3）", _smallLabel);
            _audioPath = GUILayout.TextField(_audioPath);
            if (GUILayout.Button("音声を読み込む")) StartCoroutine(LoadAudio(_audioPath));
            GUILayout.Label(_audio != null ? $"音声：{_audio.name}（{_audio.length:0.0} 秒）" : "音声：なし", _smallLabel);

            GUILayout.Space(8);
            if (GUILayout.Button("試しプレイ（P）")) TestPlay();

            if (!string.IsNullOrEmpty(_message))
            {
                GUILayout.Space(8);
                GUILayout.Label(_message, _messageLabel);
            }

            GUILayout.Space(8);
            GUILayout.Label(
                "<b>操作</b>\n" +
                "左クリック：置く／ノーツをつかんで動かす\n" +
                "右クリック：消す\n" +
                "ロング：押した所から離した所まで\n" +
                "トレイル：始点をクリック → 通過点をクリック（Shift でなめらか）→ Enter\n" +
                "F：トレイルの終点フリック\n" +
                "1〜7：ノーツの種類　[ ]：幅\n" +
                "ホイール：スクロール（Shift で速く）\n" +
                "Ctrl+ホイール：拡大・縮小\n" +
                "↑↓：1 目盛り　PageUp/Down：1 小節　Home：最初へ\n" +
                "Space：再生・停止　P：試しプレイ（Esc で戻る）\n" +
                "Ctrl+Z：元に戻す　Ctrl+Y：やり直す",
                _helpLabel);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private string LabeledField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110));
            value = GUILayout.TextField(value ?? "");
            GUILayout.EndHorizontal();
            return value;
        }

        private void ApplyHeader()
        {
            if (!float.TryParse(_levelText, NumberStyles.Float, CultureInfo.InvariantCulture, out float level))
            {
                _message = "レベルが数字ではありません。";
                return;
            }
            if (!double.TryParse(_offsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out double offset))
            {
                _message = "オフセットが数字ではありません。";
                return;
            }
            var bpm = new List<BpmChangeData>();
            foreach (var part in _bpmText.Split(',', '、'))
            {
                var pair = part.Split(':');
                if (pair.Length != 2
                    || !double.TryParse(pair[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double beat)
                    || !double.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    || value <= 0)
                {
                    _message = $"BPM「{part.Trim()}」が読めません。「0:120, 64:150」のように書きます。";
                    return;
                }
                bpm.Add(new BpmChangeData { beat = beat, value = value });
            }
            if (!bpm.Any(b => Math.Abs(b.beat) < 1e-9))
            {
                _message = "0 拍目の BPM が必要です。";
                return;
            }

            _doc.Edit(d =>
            {
                d.song = _songText;
                d.difficulty = _difficultyText;
                d.level = level;
                d.offsetMs = offset;
                d.bpm = bpm.ToArray();
            });
            RebuildTempo();
            RefreshHeaderTexts();
            _message = "曲の情報を反映しました。";
        }

        // ===== ファイル =====

        private void Save()
        {
            FinishTrail();
            try
            {
                string path = _filePath;
                if (path.EndsWith(".sus", StringComparison.OrdinalIgnoreCase))
                    path = Path.ChangeExtension(path, ".json"); // SUS では保存しない
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(_doc.Data, prettyPrint: true));
                _filePath = path;
                _doc.MarkSaved();
                _message = $"保存しました：{path}";
            }
            catch (Exception e)
            {
                _message = "保存できませんでした：" + e.Message;
            }
        }

        private void OpenFile(string path)
        {
            try
            {
                OpenText(File.ReadAllText(path), Path.GetFileName(path));
            }
            catch (IOException e)
            {
                _message = "開けませんでした：" + e.Message;
            }
            catch (UnauthorizedAccessException e)
            {
                _message = "開けませんでした：" + e.Message;
            }
        }

        private void OpenText(string text, string name)
        {
            try
            {
                ChartData data;
                if (text.TrimStart('﻿', ' ', '\t', '\r', '\n').StartsWith("{"))
                {
                    data = JsonUtility.FromJson<ChartData>(text);
                    _message = $"{name} を開きました。";
                }
                else
                {
                    var result = SusConverter.Convert(SusParser.Parse(text));
                    data = result.Data;
                    _message = $"{name} を SUS から読み込みました。保存は JSON になります。" +
                               (result.Warnings.Count > 0 ? "\n" + string.Join("\n", result.Warnings) : "");
                }
                SetDocument(new ChartDocument(data));
            }
            catch (ChartFormatException e)
            {
                _message = $"{name} を読み込めませんでした：{e.Message}";
            }
            catch (ArgumentException e)
            {
                _message = $"{name} を JSON として読めませんでした：{e.Message}";
            }
        }

        private IEnumerator LoadAudio(string path)
        {
            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN,
            };
            if (type == AudioType.UNKNOWN || !File.Exists(path))
            {
                _message = "音声ファイルが見つからないか、対応していない形式です（.wav / .ogg / .mp3）。";
                yield break;
            }

            using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _message = "音声を読み込めませんでした：" + request.error;
                    yield break;
                }
                _audio = DownloadHandlerAudioClip.GetContent(request);
                _audio.name = Path.GetFileName(path);
                _message = "音声を読み込みました。";
            }
        }

        // ===== 試しプレイ =====

        private void TestPlay()
        {
            CancelPlacing();
            if (_previewing) StopPreview();
            try
            {
                var chart = _doc.Build();
                _player.Play(chart, _audio);
            }
            catch (ChartFormatException e)
            {
                _message = "譜面に問題があるので試しプレイできません：" + e.Message;
            }
        }

        // ===== 描画の道具 =====

        private static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // 太さのある線（細長い四角を回して描く）
        private static void DrawLine(Vector2 a, Vector2 b, float width, Color color)
        {
            var dir = b - a;
            float length = dir.magnitude;
            if (length < 0.5f) return;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            Fill(new Rect(a.x, a.y - width / 2, length + 1, width), color);
            GUI.matrix = matrix;
        }
    }
}
