using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// The compression of every suspension of the active vessel at each physics step over the last seconds, drawn in a
    /// window of its own, one line per suspension: fully extended at the bottom, bottomed out at the line marked "stop".
    /// The steps are kept while the window is closed, so that opening it shows what just happened; paused, it keeps
    /// what it shows; another active vessel starts it anew.
    /// </summary>
    internal sealed class SuspensionGraph
    {
        // The window: its id, distinct from those of the other Diags, and where it first opens.
        private const int WINDOW_ID = 0x4748500B;
        private const float WINDOW_X = 20f;
        private const float WINDOW_Y = 420f;

        // The plot, in pixels, and the compression at its top: past 1, a suspension is pushed beyond its end-stop.
        private const int WIDTH = 720;
        private const int HEIGHT = 240;
        private const float TOP = 1.25f;

        // The steps kept per suspension: the longest span at 50 steps per second, with room.
        private const int CAPACITY = 1600;
        internal const double MIN_SECONDS = 1.0;
        internal const double MAX_SECONDS = 30.0;
        private static readonly double[] SPANS = { 2.0, 5.0, 10.0, 30.0 };

        // Each suspension's colour, in the order the suspensions are first read: distinct on a dark background.
        private static readonly Color32[] PALETTE =
        {
            new Color32(255, 99, 71, 255), new Color32(65, 165, 255, 255), new Color32(120, 220, 90, 255),
            new Color32(255, 200, 40, 255), new Color32(200, 110, 255, 255), new Color32(40, 220, 210, 255),
            new Color32(255, 140, 200, 255), new Color32(230, 230, 230, 255), new Color32(255, 150, 60, 255),
            new Color32(150, 150, 255, 255), new Color32(180, 255, 180, 255), new Color32(200, 170, 120, 255)
        };
        private static readonly Color32 BACKGROUND = new Color32(24, 24, 24, 255);
        private static readonly Color32 GRID = new Color32(60, 60, 60, 255);
        private static readonly Color32 ZERO = new Color32(110, 110, 110, 255);
        private static readonly Color32 STOP = new Color32(170, 60, 60, 255);

        // Static: the choices hold from one flight scene to the next, like the main window's.
        private static bool visible;
        private static Rect windowRect = new Rect(WINDOW_X, WINDOW_Y, 0f, 0f);
        private static double seconds = 10.0;

        private readonly Dictionary<uint, Series> series = new Dictionary<uint, Series>();
        private readonly List<Series> order = new List<Series>();
        private Color32[] pixels;
        private Texture2D texture;
        private System.Guid vesselId;
        private double lastTime = double.NaN;
        private bool paused;
        private bool dirty = true;
        private GUIStyle legendStyle;

        internal static bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }

        internal static Rect WindowRect
        {
            get { return windowRect; }
            set { windowRect = value; }
        }

        /// <summary>How many seconds the plot spans, kept between <see cref="MIN_SECONDS"/> and
        /// <see cref="MAX_SECONDS"/>.</summary>
        internal static double Seconds
        {
            get { return seconds; }
            set { seconds = Mathf.Clamp((float)value, (float)MIN_SECONDS, (float)MAX_SECONDS); }
        }

        /// <summary>Whether new steps are left out, the plot keeping what it shows.</summary>
        internal bool Paused
        {
            get { return paused; }
            set
            {
                paused = value;
                dirty = true;
            }
        }

        /// <summary>How many steps the suspension with the most of them keeps.</summary>
        internal int Steps
        {
            get
            {
                int most = 0;
                foreach (Series s in order)
                {
                    most = Mathf.Max(most, s.count);
                }
                return most;
            }
        }

        /// <summary>Forgets every step kept.</summary>
        internal void Clear()
        {
            series.Clear();
            order.Clear();
            lastTime = double.NaN;
            dirty = true;
        }

        /// <summary>Keeps the compression each suspension of <paramref name="vessel"/> reads at the step of game time
        /// <paramref name="time"/>, none for one off the ground; nothing while paused. Another vessel than the last
        /// one starts the plot anew.</summary>
        internal void Add(double time, Vessel vessel, List<WheelReading> wheels)
        {
            if (paused)
            {
                return;
            }
            if (vessel.id != vesselId)
            {
                Clear();
                vesselId = vessel.id;
            }
            foreach (WheelReading w in wheels)
            {
                uint id = w.Part.flightID;
                Series s;
                if (!series.TryGetValue(id, out s))
                {
                    s = new Series(id, w.Part.partInfo.title, PALETTE[order.Count % PALETTE.Length]);
                    series[id] = s;
                    order.Add(s);
                }
                s.Add(time, w.Grounded ? w.Compression : float.NaN, w.Bottomed);
            }
            lastTime = time;
            dirty = true;
        }

        /// <summary>Draws the window, when it shows. Called from OnGUI.</summary>
        internal void OnGUI(GUIStyle cellStyle)
        {
            if (!visible)
            {
                return;
            }
            if (legendStyle == null)
            {
                legendStyle = new GUIStyle(cellStyle) { clipping = TextClipping.Clip };
            }
            // The texture is drawn anew at most once per frame, and only once new steps came in.
            if (Event.current.type == EventType.Repaint && dirty)
            {
                Redraw();
            }
            windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "KSP Diag - Suspension graph");
        }

        /// <summary>Releases the texture.</summary>
        internal void Destroy()
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Compression of each suspension at each physics step, last", GUILayout.ExpandWidth(false));
            foreach (double span in SPANS)
            {
                bool selected = Mathf.Abs((float)(span - seconds)) < 1e-3f;
                if (GUILayout.Toggle(selected, span.ToString("0", CultureInfo.InvariantCulture) + " s", "Button",
                        GUILayout.Width(50f)) && !selected)
                {
                    seconds = span;
                    dirty = true;
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(paused ? "Resume" : "Pause", GUILayout.Width(80f)))
            {
                Paused = !paused;
            }
            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                Clear();
            }
            GUILayout.EndHorizontal();

            // The plot, with room on its left for the compression marks and under it for the time marks.
            Rect area = GUILayoutUtility.GetRect(WIDTH + 50f, HEIGHT + 18f, GUILayout.ExpandWidth(false));
            Rect plot = new Rect(area.x + 50f, area.y, WIDTH, HEIGHT);
            if (texture != null)
            {
                GUI.DrawTexture(plot, texture);
            }
            YMark(area, plot, 0f, "0 ext.");
            YMark(area, plot, 0.5f, "0.5");
            YMark(area, plot, 1f, "1 stop");
            YMark(area, plot, TOP, TOP.ToString("0.00", CultureInfo.InvariantCulture));
            GUI.Label(new Rect(plot.x, plot.yMax, 80f, 18f), "-" + seconds.ToString("0", CultureInfo.InvariantCulture)
                + " s", legendStyle);
            GUI.Label(new Rect(plot.x + WIDTH / 2f - 20f, plot.yMax, 80f, 18f), "-"
                + (seconds / 2).ToString("0.#", CultureInfo.InvariantCulture) + " s", legendStyle);
            GUI.Label(new Rect(plot.xMax - 60f, plot.yMax, 60f, 18f), paused ? "paused" : "now", legendStyle);

            // The legend, in each line's colour: the last digits of the part's flight id, its last compression, its
            // title.
            const int perRow = 3;
            for (int i = 0; i < order.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, order.Count); j++)
                {
                    Series s = order[j];
                    legendStyle.normal.textColor = s.color;
                    GUILayout.Label("#" + (s.id % 10000).ToString("0000", CultureInfo.InvariantCulture) + " "
                        + (float.IsNaN(s.last) ? "air" : (100f * s.last).ToString("0", CultureInfo.InvariantCulture)
                            + " %" + (s.lastBottomed ? " stop" : "")) + "  " + s.title, legendStyle,
                        GUILayout.Width((WIDTH + 50f) / perRow - 4f));
                }
                GUILayout.EndHorizontal();
            }
            legendStyle.normal.textColor = Color.white;
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void YMark(Rect area, Rect plot, float compression, string text)
        {
            float y = plot.yMax - compression / TOP * (HEIGHT - 1) - 8f;
            legendStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(area.x, y, 48f, 16f), text, legendStyle);
        }

        /// <summary>Draws the kept steps of the span into the texture.</summary>
        private void Redraw()
        {
            dirty = false;
            if (texture == null)
            {
                texture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixels = new Color32[WIDTH * HEIGHT];
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = BACKGROUND;
            }
            // Grid: a compression line every quarter of the travel, the end-stop brighter; a time line every
            // second, every five over the longest span.
            for (float c = 0.25f; c < TOP; c += 0.25f)
            {
                HLine(Row(c), Mathf.Abs(c - 1f) < 1e-3f ? STOP : GRID);
            }
            HLine(Row(0f), ZERO);
            double step = seconds > 10.0 ? 5.0 : 1.0;
            for (double back = step; back < seconds; back += step)
            {
                VLine(Column(-back), GRID);
            }
            if (!double.IsNaN(lastTime))
            {
                double start = lastTime - seconds;
                // Two steps further apart than this are not joined: steps were missed in between.
                double gap = 2.5 * Time.fixedDeltaTime;
                foreach (Series s in order)
                {
                    int px = -1, py = -1;
                    double pt = double.NaN;
                    for (int k = 0; k < s.count; k++)
                    {
                        int i = (s.start + k) % CAPACITY;
                        double t = s.times[i];
                        float v = s.values[i];
                        if (t < start || float.IsNaN(v))
                        {
                            px = -1;
                            continue;
                        }
                        int x = Column(t - lastTime);
                        int y = Row(Mathf.Clamp(v, 0f, TOP));
                        if (px >= 0 && t - pt <= gap)
                        {
                            Line(px, py, x, y, s.color);
                        }
                        else
                        {
                            Plot(x, y, s.color);
                        }
                        px = x;
                        py = y;
                        pt = t;
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        // The pixel column of a time relative to the last step (0 or less), and the pixel row of a compression; the
        // texture's first row is its bottom.
        private int Column(double relative)
        {
            return Mathf.Clamp((int)((1.0 + relative / seconds) * (WIDTH - 1)), 0, WIDTH - 1);
        }

        private static int Row(float compression)
        {
            return Mathf.Clamp(Mathf.RoundToInt(compression / TOP * (HEIGHT - 1)), 0, HEIGHT - 1);
        }

        private void HLine(int y, Color32 color)
        {
            for (int x = 0; x < WIDTH; x++)
            {
                pixels[y * WIDTH + x] = color;
            }
        }

        private void VLine(int x, Color32 color)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                pixels[y * WIDTH + x] = color;
            }
        }

        // A point two pixels high, so that a line along the grid still shows.
        private void Plot(int x, int y, Color32 color)
        {
            pixels[y * WIDTH + x] = color;
            if (y + 1 < HEIGHT)
            {
                pixels[(y + 1) * WIDTH + x] = color;
            }
        }

        private void Line(int x0, int y0, int x1, int y1, Color32 color)
        {
            // Bresenham, every pixel of the segment.
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }
                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>The steps kept for one suspension, oldest first, in a ring.</summary>
        private sealed class Series
        {
            internal readonly uint id;
            internal readonly string title;
            internal readonly Color32 color;
            internal readonly double[] times = new double[CAPACITY];
            internal readonly float[] values = new float[CAPACITY];
            internal int start;
            internal int count;
            internal float last = float.NaN;
            internal bool lastBottomed;

            internal Series(uint id, string title, Color32 color)
            {
                this.id = id;
                this.title = title;
                this.color = color;
            }

            /// <summary>Keeps one step, the oldest one dropped when the ring is full.</summary>
            internal void Add(double time, float value, bool bottomed)
            {
                int i = (start + count) % CAPACITY;
                if (count == CAPACITY)
                {
                    start = (start + 1) % CAPACITY;
                }
                else
                {
                    count++;
                }
                times[i] = time;
                values[i] = value;
                last = value;
                lastBottomed = bottomed;
            }
        }
    }
}
