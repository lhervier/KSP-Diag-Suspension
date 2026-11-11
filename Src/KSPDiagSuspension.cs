using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// Suspension viewer. In flight, after each physics step, it reads what the game computed for every wheel,
    /// leg and landing gear of the active vessel: whether it touches the ground, how far its suspension is
    /// compressed (bottomed out, fully extended), its spring and damper, the load it carries and the load its
    /// spring was sized for, and the tyre force against the largest the tyre gives while it holds; and, for the
    /// whole vessel, the share of its weight the suspensions carry and the tyre grip against the pull of the
    /// slope. Slipping is judged by forces only, never by a speed. It also shows the speed of the vessel over
    /// the ground in mm/s, for the record. A window shows all this; a CSV file records it step by step. Nothing
    /// is changed in the game.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KSPDiagSuspension : MonoBehaviour
    {
        private const string LOG_PREFIX = "[KSPDiagSuspension] ";

        // The window: its id, distinct from those of the other Diags, its width, and where it first opens.
        private const int WINDOW_ID = 0x4748500A;
        private const float WINDOW_WIDTH = 1010f;
        private const float WINDOW_X = 20f;
        private const float WINDOW_Y = 40f;
        private const float LIST_HEIGHT = 220f;

        // Mod+F6 shows or hides the window, the same key for every KSP Diag. Static: the choice holds from one
        // flight scene to the next, and so does where the window is.
        private static readonly KeyBinding WINDOW_KEY = new KeyBinding(KeyCode.F6);
        private static bool windowVisible = true;
        private static Rect windowRect = new Rect(WINDOW_X, WINDOW_Y, WINDOW_WIDTH, 0f);

        // Whether the static-grip anchors of KSP-Fix-SuspensionBounce are drawn under the wheels (window toggle,
        // off at first). Static: the choice holds from one flight scene to the next.
        private static bool drawAnchors;

        private readonly List<WheelReading> wheels = new List<WheelReading>();
        private readonly SpeedTracker speed = new SpeedTracker();
        private readonly Recorder recorder = new Recorder();
        private AnchorDrawing anchorDrawing;
        private VesselBalance balance;
        private Vessel vessel;
        private double readTime = double.NaN;
        private Vector2 listScroll;

        internal static bool WindowVisible
        {
            get { return windowVisible; }
            set { windowVisible = value; }
        }

        internal Rect WindowRect
        {
            get { return windowRect; }
            set { windowRect = value; }
        }

        internal Recorder Recorder { get { return recorder; } }

        private void Start()
        {
            StartCoroutine(ReadAfterPhysics());
        }

        private void OnDestroy()
        {
            recorder.Stop();
            if (anchorDrawing != null)
            {
                anchorDrawing.Destroy();
            }
        }

        private void LateUpdate()
        {
            if (!drawAnchors || !AnchorReader.Available || balance == null)
            {
                return;
            }
            if (anchorDrawing == null)
            {
                anchorDrawing = new AnchorDrawing();
            }
            anchorDrawing.Draw(readTime, wheels);
        }

        private void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && WINDOW_KEY.GetKeyDown())
            {
                windowVisible = !windowVisible;
            }
        }

        /// <summary>Reads the wheels right after each physics step, when the game has applied this step's
        /// forces.</summary>
        private IEnumerator ReadAfterPhysics()
        {
            WaitForFixedUpdate wait = new WaitForFixedUpdate();
            while (true)
            {
                yield return wait;
                ReadStep();
            }
        }

        private void ReadStep()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            wheels.Clear();
            balance = null;
            vessel = v;
            if (v == null || !v.loaded || v.packed)
            {
                speed.Clear();
                return;
            }
            foreach (Part part in v.parts)
            {
                ModuleWheelBase wheelBase = part.FindModuleImplementing<ModuleWheelBase>();
                if (wheelBase == null)
                {
                    continue;
                }
                WheelReading reading = WheelReading.Read(wheelBase);
                if (reading != null)
                {
                    wheels.Add(reading);
                }
            }
            readTime = Time.fixedTime;
            speed.Add(v, readTime);
            balance = VesselBalance.Compute(v, wheels);
            if (anchorDrawing != null)
            {
                anchorDrawing.NoteStep(readTime, wheels);
            }
            recorder.Write(readTime, v, speed, balance, wheels);
        }

        /// <summary>Starts recording into a new CSV file; returns its path.</summary>
        internal string StartRecording()
        {
            string path = recorder.Start();
            Debug.Log(LOG_PREFIX + "recording into " + path);
            return path;
        }

        /// <summary>Stops recording; returns the path of the file written, null when none.</summary>
        internal string StopRecording()
        {
            bool was = recorder.Recording;
            recorder.Stop();
            if (was)
            {
                Debug.Log(LOG_PREFIX + "recorded " + recorder.Lines + " lines into " + recorder.FilePath);
            }
            return recorder.FilePath;
        }

        /// <summary>What the window shows, for the MCP tools.</summary>
        internal Dictionary<string, object> Read()
        {
            Dictionary<string, object> result = new Dictionary<string, object>
            {
                { "vessel", vessel != null ? vessel.vesselName : "" },
                { "time", Num(readTime) },
                { "speedMmS", Num(speed.SpeedMmS) },
                { "yawRateDegS", Num(speed.YawRateDegS) },
                { "lateralMS", Num(speed.LateralMS) },
                { "driftDeg", Num(speed.DriftDeg) },
                { "recording", recorder.Recording },
                { "csv", recorder.FilePath ?? "" },
                { "csvLines", recorder.Lines }
            };
            if (balance != null)
            {
                result["weightKn"] = Num(balance.WeightKn);
                result["slopeDeg"] = Num(balance.SlopeDeg);
                result["normalWeightKn"] = Num(balance.NormalWeightKn);
                result["tangentialWeightKn"] = Num(balance.TangentialWeightKn);
                result["supportedKn"] = Num(balance.SupportedKn);
                result["supportedPct"] = Num(balance.SupportedPct);
                result["capKn"] = Num(balance.CapKn);
                result["tireForceKn"] = Num(balance.TireForceKn);
                result["gripMargin"] = Num(balance.GripMargin);
                result["grounded"] = balance.Grounded;
                result["bottomed"] = balance.Bottomed;
            }
            List<object> list = new List<object>();
            foreach (WheelReading w in wheels)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "part", w.Part.partInfo.title },
                    { "partName", w.Part.partInfo.name },
                    { "partId", (double)w.Part.flightID },
                    { "grounded", w.Grounded },
                    { "compression", Num(w.Compression) },
                    { "travelM", Num(w.TravelM) },
                    { "bottomed", w.Bottomed },
                    { "extended", w.Extended },
                    { "spring", Num(w.Spring) },
                    { "damper", Num(w.Damper) },
                    { "loadKn", Num(w.LoadKn) },
                    { "frictionLoadKn", Num(w.FrictionLoadKn) },
                    { "assumedLoadKn", Num(w.AssumedLoadKn) },
                    { "grip", Num(w.Grip) },
                    { "frictionModel", w.FrictionModel },
                    { "adherentCoef", Num(w.AdherentCoef) },
                    { "capKn", Num(w.CapKn) },
                    { "tireSideKn", Num(w.TireForce.x) },
                    { "tireFwdKn", Num(w.TireForce.y) },
                    { "usedPct", Num(w.UsedPct) },
                    { "regime", w.Regime },
                    { "angularVelocity", Num(w.AngularVelocity) },
                    { "radiusM", Num(w.RadiusM) },
                    { "rimSpeedMmS", Num(w.RimSpeedMmS) },
                    { "anchor", w.HasAnchor },
                    { "anchorOffsetX", w.HasAnchor ? Num(w.AnchorOffset.x) : null },
                    { "anchorOffsetY", w.HasAnchor ? Num(w.AnchorOffset.y) : null },
                    { "anchorForceX", w.HasAnchor ? Num(w.AnchorForce.x) : null },
                    { "anchorForceY", w.HasAnchor ? Num(w.AnchorForce.y) : null },
                    { "anchorGain", w.HasAnchor ? Num(w.AnchorGain) : null },
                    { "anchorFade", w.HasAnchor ? Num(w.AnchorFade) : null },
                    { "anchorReseated", w.HasAnchor && w.AnchorReseated },
                    { "anchorState", w.AnchorState }
                });
            }
            result["wheels"] = list;
            return result;
        }

        private static object Num(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? null : (object)v;
        }

        private void OnGUI()
        {
            if (!windowVisible)
            {
                return;
            }
            GUI.skin = HighLogic.Skin;
            windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "KSP Diag - Suspension");
        }

        private static readonly float[] COLUMNS =
        {
            210f, 50f, 85f, 50f, 50f, 60f, 60f, 50f, 55f, 60f, 60f, 80f, 75f, 100f, 100f, 55f
        };
        private static readonly string[] HEADERS =
        {
            "Part", "Ground", "Compression", "Spring", "Damper", "Load kN", "Sized kN", "μ adh", "Cap kN",
            "Tyre kN", "Used", "Regime", "Rim mm/s", "Anchor mm", "Anchor kN", "Anchor"
        };

        // The anchor columns, shown only when KSP-Fix-SuspensionBounce is loaded: the window widens for them.
        private const int ANCHOR_COLUMNS = 3;

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            if (vessel == null || balance == null)
            {
                GUILayout.Label("No loaded vessel in physics.");
            }
            else
            {
                GUILayout.Label(vessel.vesselName + ": speed " + Fmt(speed.SpeedMmS, "0.00") + " mm/s over "
                    + SpeedTracker.WINDOW.ToString("0.#", CultureInfo.InvariantCulture) + " s, turning "
                    + Fmt(speed.YawRateDegS, "0.00") + " °/s, sideways " + Fmt(speed.LateralMS, "0.000")
                    + " m/s (drift " + Fmt(speed.DriftDeg, "0.0") + "°), weight "
                    + Fmt(balance.WeightKn, "0.00") + " kN, slope " + Fmt(balance.SlopeDeg, "0.0") + "°");
                GUILayout.Label("On the ground " + balance.Grounded + "/" + balance.Wheels + ", bottomed out "
                    + balance.Bottomed + ". Suspensions carry " + Fmt(balance.SupportedKn, "0.00") + " of "
                    + Fmt(balance.NormalWeightKn, "0.00") + " kN pressing on the ground ("
                    + Fmt(balance.SupportedPct, "0.0") + " %)");
                GUILayout.Label("Tyre grip " + Fmt(balance.CapKn, "0.00") + " kN against a pull down the slope of "
                    + Fmt(balance.TangentialWeightKn, "0.00") + " kN (x" + Fmt(balance.GripMargin, "0.00")
                    + "); tyre forces applied " + Fmt(balance.TireForceKn, "0.00") + " kN");

                int columns = AnchorReader.Available ? HEADERS.Length : HEADERS.Length - ANCHOR_COLUMNS;
                GUILayout.BeginHorizontal();
                for (int i = 0; i < columns; i++)
                {
                    GUILayout.Label(HEADERS[i], GUILayout.Width(COLUMNS[i]));
                }
                GUILayout.EndHorizontal();
                listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(LIST_HEIGHT));
                foreach (WheelReading w in wheels)
                {
                    GUILayout.BeginHorizontal();
                    Cell(0, w.Part.partInfo.title);
                    Cell(1, w.Grounded ? "yes" : "no");
                    Cell(2, !w.Grounded ? "-" : Fmt(100.0 * w.Compression, "0") + " %"
                        + (w.Bottomed ? " stop" : w.Extended ? " ext." : ""));
                    Cell(3, Fmt(w.Spring, "0.0"));
                    Cell(4, Fmt(w.Damper, "0.0"));
                    Cell(5, Fmt(w.LoadKn, "0.00"));
                    Cell(6, Fmt(w.AssumedLoadKn, "0.00"));
                    Cell(7, Fmt(w.AdherentCoef, "0.000"));
                    Cell(8, Fmt(w.CapKn, "0.00") + (CapRemark(w) ? " *" : ""));
                    Cell(9, w.Grounded ? Fmt(w.TireForce.magnitude, "0.00") : "-");
                    Cell(10, w.Grounded ? Fmt(w.UsedPct, "0.0") + " %" : "-");
                    Cell(11, w.Regime);
                    Cell(12, Fmt(w.RimSpeedMmS, "0.00"));
                    if (columns > 13)
                    {
                        // Offset (mm) and force (kN) of the anchor, sideways then forward.
                        Cell(13, !w.HasAnchor ? "-" : Fmt(1000.0 * w.AnchorOffset.x, "0.0") + ", "
                            + Fmt(1000.0 * w.AnchorOffset.y, "0.0"));
                        Cell(14, !w.HasAnchor ? "-" : Fmt(w.AnchorForce.x, "0.000") + ", "
                            + Fmt(w.AnchorForce.y, "0.000"));
                        Cell(15, w.AnchorState);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                foreach (WheelReading w in wheels)
                {
                    if (CapRemark(w))
                    {
                        GUILayout.Label("* " + w.Part.partInfo.title + ": grip " + Fmt(w.Grip, "0.000")
                            + ", tyre model load " + Fmt(w.FrictionLoadKn, "0.00") + " kN");
                    }
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(recorder.Recording ? "Stop recording" : "Record CSV", GUILayout.Width(150f)))
            {
                if (recorder.Recording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }
            if (recorder.FilePath != null)
            {
                GUILayout.Label(System.IO.Path.GetFileName(recorder.FilePath) + ", " + recorder.Lines + " lines"
                    + (recorder.Recording ? " (recording)" : ""));
            }
            GUILayout.FlexibleSpace();
            if (AnchorReader.Available)
            {
                drawAnchors = GUILayout.Toggle(drawAnchors, "Draw anchors (arrow "
                    + AnchorDrawing.FORCE_SCALE.ToString("0.#", CultureInfo.InvariantCulture) + " m/kN)");
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        /// <summary>Whether the cap of a wheel needs more than Load × μ adh: a grip other than 1, or a tyre model
        /// load other than the suspension force (extra or minimum downforce).</summary>
        private static bool CapRemark(WheelReading w)
        {
            return w.Grounded && (Mathf.Abs(w.Grip - 1f) > 1e-3f
                || Mathf.Abs(w.FrictionLoadKn - w.LoadKn) > 0.01f * Mathf.Max(w.LoadKn, 0.01f));
        }

        private static void Cell(int column, string text)
        {
            GUILayout.Label(text, GUILayout.Width(COLUMNS[column]));
        }

        private static string Fmt(double v, string format)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
