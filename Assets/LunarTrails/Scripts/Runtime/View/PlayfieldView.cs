using System.Collections.Generic;
using LunarTrails.Core.Charts;
using UnityEngine;

namespace LunarTrails.Runtime.View
{
    /// <summary>
    /// 試作用の仮の見た目：レーンとノーツを Unity 標準の立方体で描く。
    /// </summary>
    /// <remarks>
    /// 画面の並びは企画書どおり「左にキーレーン 3 本、中央に幅の広いマウスレーン、右にキーレーン 3 本」。
    /// ノーツは奥（+Z）から手前の判定ライン（Z = 0）へ流れてくる。
    /// UI 案が決まったら、このクラスごと本番の表示に置き換える。判定や時計のコードは変えなくてよい。
    ///
    /// 色は企画書のノーツの色（タップ青、トラック黄、フリック赤、トレイルオレンジ、ガイド白）だけ合わせている。
    /// トレイルの軌道は Core の TrailPath で計算するので、判定と同じ形になる。
    /// </remarks>
    public sealed class PlayfieldView : MonoBehaviour
    {
        // ===== 配置（ワールド座標の単位） =====
        private const float KeyLaneWidth = 1f;
        private const float MouseLaneWidth = 4f;
        private const float LaneLength = 40f;
        private const float NoteThickness = 0.1f;
        private const float NoteDepth = 0.3f;
        private const int TrailSmoothSteps = 8;

        [Tooltip("ノーツの流れる速さ（1 秒に進む距離）")]
        [SerializeField] private float _scrollSpeed = 16f;

        private readonly List<NoteObject> _notes = new List<NoteObject>();
        private readonly Renderer[] _keyLaneRenderers = new Renderer[Chart.KeyLaneCount];
        private Material _laneMaterial;
        private Material _laneHeldMaterial;
        private Transform _noteRoot;
        private Transform _cursor;

        private sealed class NoteObject
        {
            public Note Note;
            public Transform Head;               // ノーツ本体（トレイルでは null）
            public Transform LongBody;           // ロングの帯
            public List<Transform> TrailSegments; // トレイルを細かく区切った区間
            public double[] TrailSampleTimes;     // 区間の境目の時刻（TrailSegments より 1 つ多い）
        }

        /// <summary>ノーツの流れる速さ（1 秒に進む距離）</summary>
        public float ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary>ノーツを全部消す（メニューに戻ったとき用）。</summary>
        public void ClearNotes()
        {
            if (_noteRoot != null) Destroy(_noteRoot.gameObject);
            _noteRoot = null;
            _notes.Clear();
        }

        // ===== レーン配置の計算 =====

        /// <summary>キーレーンの中心の X 座標。0〜2 は左、3〜5 は右。</summary>
        public static float KeyLaneCenterX(int lane)
        {
            float edge = MouseLaneWidth / 2f;
            return lane < 3
                ? -edge - KeyLaneWidth * (2.5f - lane)
                : edge + KeyLaneWidth * (lane - 2.5f);
        }

        /// <summary>マウスレーン内の位置（0 = 左端、1 = 右端）を X 座標に直す。</summary>
        public static float MouseLaneX(float x) => -MouseLaneWidth / 2f + MouseLaneWidth * x;

        // ===== 組み立て =====

        /// <summary>レーンと判定ラインを作り、カメラを置く。最初に 1 回だけ呼ぶ。</summary>
        public void BuildStage(Camera camera)
        {
            _laneMaterial = Materials.Get(new Color(0.15f, 0.15f, 0.18f));
            _laneHeldMaterial = Materials.Get(new Color(0.35f, 0.35f, 0.45f));

            for (int lane = 0; lane < Chart.KeyLaneCount; lane++)
            {
                var r = CreateBox("KeyLane" + lane, transform,
                    new Vector3(KeyLaneCenterX(lane), -0.05f, LaneLength / 2f),
                    new Vector3(KeyLaneWidth * 0.95f, 0.02f, LaneLength), _laneMaterial);
                _keyLaneRenderers[lane] = r;
            }
            CreateBox("MouseLane", transform,
                new Vector3(0, -0.05f, LaneLength / 2f),
                new Vector3(MouseLaneWidth * 0.98f, 0.02f, LaneLength),
                Materials.Get(new Color(0.1f, 0.1f, 0.12f)));

            // マウスレーンのカーソル（判定ラインの上に置く白い板）
            _cursor = CreateBox("MouseCursor", transform, new Vector3(0, 0.08f, 0),
                new Vector3(0.12f, 0.06f, 0.6f), Materials.Get(new Color(0.9f, 0.9f, 0.9f))).transform;

            float totalWidth = MouseLaneWidth + KeyLaneWidth * 6;
            CreateBox("JudgeLine", transform, Vector3.zero,
                new Vector3(totalWidth, 0.05f, 0.08f), Materials.Get(Color.white));

            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 7f, -6f);
                camera.transform.LookAt(new Vector3(0, 0, 9f));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }
        }

        /// <summary>譜面のノーツをすべて作る（試作なので、出し入れではなく最初に全部作る）。</summary>
        public void BuildNotes(Chart chart)
        {
            if (_noteRoot != null) Destroy(_noteRoot.gameObject);
            _notes.Clear();
            _noteRoot = new GameObject("Notes").transform;
            _noteRoot.SetParent(transform, false);

            foreach (var note in chart.Notes)
                _notes.Add(CreateNoteObject(note));
        }

        // ===== 毎フレームの更新 =====

        /// <param name="gameTime">ゲームの時刻（秒）。オフセット調整済みのもの。</param>
        /// <param name="isJudged">判定済み（もう表示しない）ノーツかを返す関数</param>
        /// <param name="isLaneHeld">キーレーンが押されているかを返す関数</param>
        /// <param name="cursorX">マウスレーン内のカーソル位置（0 = 左端、1 = 右端）</param>
        public void Render(double gameTime, System.Func<Note, bool> isJudged, System.Func<int, bool> isLaneHeld, float cursorX)
        {
            SetX(_cursor, MouseLaneX(cursorX));

            for (int lane = 0; lane < Chart.KeyLaneCount; lane++)
                _keyLaneRenderers[lane].sharedMaterial = isLaneHeld(lane) ? _laneHeldMaterial : _laneMaterial;

            double visibleSeconds = LaneLength / _scrollSpeed;

            foreach (var obj in _notes)
            {
                var note = obj.Note;
                double untilHit = note.Time - gameTime;
                bool inRange = untilHit < visibleSeconds && gameTime < note.EndTime + 0.2;

                if (obj.Head != null)
                {
                    // トレイルの頭は終点のフリック。判定済みかどうかは見ず、時刻を過ぎたら消す
                    bool isTrail = note.Type == NoteType.Trail;
                    double headTime = isTrail ? note.EndTime : note.Time;
                    double untilHead = headTime - gameTime;
                    bool show = untilHead < visibleSeconds && untilHead > -0.2 && (isTrail || !isJudged(note));
                    obj.Head.gameObject.SetActive(show);
                    if (show) SetZ(obj.Head, ZAt(headTime, gameTime));
                }

                if (obj.LongBody != null)
                {
                    // 終点の判定はないので、帯は終わりの時刻まで出しておく（判定ラインより手前は切る）
                    bool show = inRange && gameTime < note.EndTime;
                    obj.LongBody.gameObject.SetActive(show);
                    if (show)
                    {
                        float start = Mathf.Max(0f, ZAt(note.Time, gameTime));
                        float end = Mathf.Min(LaneLength, ZAt(note.EndTime, gameTime));
                        PlaceAlongZ(obj.LongBody, start, end);
                    }
                }

                if (obj.TrailSegments != null)
                {
                    var times = obj.TrailSampleTimes;
                    for (int i = 0; i < obj.TrailSegments.Count; i++)
                    {
                        var seg = obj.TrailSegments[i];
                        double ta = times[i], tb = times[i + 1];
                        bool show = gameTime < tb && ta - gameTime < visibleSeconds;
                        seg.gameObject.SetActive(show);
                        if (show)
                        {
                            // 判定ラインより手前に出た部分は切る
                            double from = System.Math.Max(ta, gameTime);
                            var a = new Vector3(MouseLaneX(TrailPath.XAt(note, from)), 0, ZAt(from, gameTime));
                            var b = new Vector3(MouseLaneX(TrailPath.XAt(note, tb)), 0, ZAt(tb, gameTime));
                            PlaceBetween(seg, a, b, note.Size * MouseLaneWidth);
                        }
                    }
                }
            }
        }

        // ===== ノーツの生成 =====

        private NoteObject CreateNoteObject(Note note)
        {
            var obj = new NoteObject { Note = note };
            string name = note.ToString();

            switch (note.Type)
            {
                case NoteType.Tap:
                case NoteType.Long:
                case NoteType.Guide:
                {
                    float x = (KeyLaneCenterX(note.Lane) + KeyLaneCenterX(note.Lane + note.Width - 1)) / 2f;
                    float width = KeyLaneWidth * note.Width * 0.9f;
                    var color = note.Type == NoteType.Guide ? Color.white : new Color(0.2f, 0.5f, 1f);
                    float height = note.Type == NoteType.Guide ? 0.02f : NoteThickness;
                    obj.Head = CreateBox(name, _noteRoot, new Vector3(x, 0, 0),
                        new Vector3(width, height, NoteDepth), Materials.Get(color)).transform;

                    if (note.Type == NoteType.Long)
                    {
                        obj.LongBody = CreateBox(name + " body", _noteRoot, new Vector3(x, 0, 0),
                            new Vector3(width * 0.8f, 0.04f, 1f), Materials.Get(new Color(0.1f, 0.25f, 0.55f))).transform;
                    }
                    break;
                }
                case NoteType.Click:
                    obj.Head = CreateBox(name, _noteRoot, Vector3.zero,
                        new Vector3(MouseLaneWidth * 0.95f, NoteThickness, NoteDepth), Materials.Get(new Color(0.2f, 0.5f, 1f))).transform;
                    break;
                case NoteType.Track:
                case NoteType.Flick:
                {
                    var color = note.Type == NoteType.Track ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.2f, 0.2f);
                    obj.Head = CreateBox(name, _noteRoot, new Vector3(MouseLaneX(note.X), 0, 0),
                        new Vector3(Mathf.Max(0.1f, note.Size * MouseLaneWidth), NoteThickness, NoteDepth), Materials.Get(color)).transform;
                    break;
                }
                case NoteType.Trail:
                {
                    // 曲線を細かい直線の区間に分けて描く（直線の区間は 1 つ、なめらかな区間は TrailSmoothSteps 個）
                    var times = new List<double> { note.TrailPoints[0].Time };
                    for (int i = 1; i < note.TrailPoints.Count; i++)
                    {
                        var a = note.TrailPoints[i - 1];
                        var b = note.TrailPoints[i];
                        int steps = b.Curve == CurveType.Smooth ? TrailSmoothSteps : 1;
                        for (int k = 1; k <= steps; k++)
                            times.Add(a.Time + (b.Time - a.Time) * k / steps);
                    }
                    obj.TrailSampleTimes = times.ToArray();
                    obj.TrailSegments = new List<Transform>();
                    var trailMaterial = Materials.Get(new Color(1f, 0.55f, 0.1f));
                    for (int i = 0; i < times.Count - 1; i++)
                        obj.TrailSegments.Add(CreateBox($"{name} seg{i}", _noteRoot, Vector3.zero, Vector3.one, trailMaterial).transform);

                    // 終点のフリック（あるときだけ）は、普通のフリックと同じ赤い板で終点に置く
                    if (note.EndFlick)
                    {
                        var last = note.TrailPoints[note.TrailPoints.Count - 1];
                        obj.Head = CreateBox(name + " end flick", _noteRoot, new Vector3(MouseLaneX(last.X), 0.02f, 0),
                            new Vector3(Mathf.Max(0.1f, note.Size * MouseLaneWidth), NoteThickness, NoteDepth),
                            Materials.Get(new Color(1f, 0.2f, 0.2f))).transform;
                    }
                    break;
                }
            }
            return obj;
        }

        // ===== 小さな道具 =====

        private float ZAt(double noteTime, double gameTime) => (float)((noteTime - gameTime) * _scrollSpeed);

        private static void SetX(Transform t, float x)
        {
            var p = t.localPosition;
            t.localPosition = new Vector3(x, p.y, p.z);
        }

        private static void SetZ(Transform t, float z)
        {
            var p = t.localPosition;
            t.localPosition = new Vector3(p.x, p.y, z);
        }

        private static void PlaceAlongZ(Transform t, float startZ, float endZ)
        {
            var p = t.localPosition;
            var s = t.localScale;
            t.localPosition = new Vector3(p.x, p.y, (startZ + endZ) / 2f);
            t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.01f, endZ - startZ));
        }

        private static void PlaceBetween(Transform t, Vector3 from, Vector3 to, float width)
        {
            var dir = to - from;
            t.localPosition = (from + to) / 2f;
            t.localRotation = dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            t.localScale = new Vector3(Mathf.Max(0.1f, width), 0.05f, dir.magnitude);
        }

        private static Renderer CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>()); // 当たり判定は使わない
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>色ごとに 1 つだけマテリアルを作って使い回す。ライトの影響を受けない Unlit を使う。</summary>
        private static class Materials
        {
            private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();

            public static Material Get(Color color)
            {
                if (Cache.TryGetValue(color, out var m) && m != null) return m;

                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m = new Material(shader) { color = color };
                Cache[color] = m;
                return m;
            }
        }
    }
}
