using System;
using System.Collections.Generic;
using UnityEngine;
using com.github.lhervier.ksp.util.mcpserver;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// What KSP-Util-MCPServer, when it is installed, offers of this mod as tools: what its window shows, the
    /// CSV recording, and the window itself. Each tool works on the mod loaded in the flight scene, but the
    /// showing of the window. This mod runs the same without KSP-Util-MCPServer: only that server reads the
    /// attribute.
    /// </summary>
    internal static class McpTools
    {
        [McpTool("suspension_read",
            "Reads what KSP Diag - Suspension shows for the active vessel at the last physics step (kN): speeds, " +
            "weight, slope, support and grip totals, CSV state; per suspension: contact, compression, spring, " +
            "damper, loads, tyre cap, forces, regime, wheel rotation, and anchor fields when published.")]
        internal static object Read()
        {
            return Mod().Read();
        }

        [McpTool("suspension_record_start",
            "Starts recording KSP Diag - Suspension into a new CSV file, as its button Record CSV does: one line " +
            "per suspension at each physics step, the vessel columns repeated on each, closing the file being " +
            "written if any. Returns the path of the new file (csv).")]
        internal static object RecordStart()
        {
            return new Dictionary<string, object> { { "csv", Mod().StartRecording() } };
        }

        [McpTool("suspension_record_stop",
            "Stops the CSV recording of KSP Diag - Suspension, as its button Stop recording does. Returns the path " +
            "of the file written (csv, empty when none was ever started) and its lines (csvLines).")]
        internal static object RecordStop()
        {
            KSPDiagSuspension mod = Mod();
            string path = mod.StopRecording();
            return new Dictionary<string, object>
            {
                { "csv", path ?? "" },
                { "csvLines", mod.Recorder.Lines }
            };
        }

        [McpTool("suspension_move_window",
            "Moves the window of KSP Diag - Suspension, as dragging it does: x and y in pixels from the top left " +
            "corner of the screen. Returns its position and size (x, y, width, height).")]
        internal static object MoveWindow(double x, double y)
        {
            KSPDiagSuspension mod = Mod();
            Rect rect = mod.WindowRect;
            rect.x = (float)x;
            rect.y = (float)y;
            mod.WindowRect = rect;
            return new Dictionary<string, object>
            {
                { "x", (double)rect.x },
                { "y", (double)rect.y },
                { "width", (double)rect.width },
                { "height", (double)rect.height }
            };
        }

        [McpTool("suspension_show_window",
            "Shows or hides the window of KSP Diag - Suspension, as Mod+F6 does; the readings and the recording " +
            "go on either way. Returns whether it shows (visible).")]
        internal static object ShowWindow(bool visible)
        {
            KSPDiagSuspension.WindowVisible = visible;
            return new Dictionary<string, object> { { "visible", KSPDiagSuspension.WindowVisible } };
        }

        [McpTool("suspension_show_graph",
            "Shows or hides the graph window of KSP Diag - Suspension, as its Graph button does: each " +
            "suspension's compression at each physics step (stop line at 1). Optionally sets its span (seconds, 1 " +
            "to 30) and position (x, y). Returns visible, seconds, paused, x, y, width, height.")]
        internal static object ShowGraph(bool visible, double seconds = 0, double x = double.NaN,
            double y = double.NaN)
        {
            SuspensionGraph.Visible = visible;
            if (seconds > 0)
            {
                SuspensionGraph.Seconds = seconds;
            }
            Rect rect = SuspensionGraph.WindowRect;
            if (!double.IsNaN(x))
            {
                rect.x = (float)x;
            }
            if (!double.IsNaN(y))
            {
                rect.y = (float)y;
            }
            SuspensionGraph.WindowRect = rect;
            KSPDiagSuspension mod = UnityEngine.Object.FindObjectOfType<KSPDiagSuspension>();
            return new Dictionary<string, object>
            {
                { "visible", SuspensionGraph.Visible },
                { "seconds", SuspensionGraph.Seconds },
                { "paused", mod != null && mod.Graph.Paused },
                { "x", (double)rect.x },
                { "y", (double)rect.y },
                { "width", (double)rect.width },
                { "height", (double)rect.height }
            };
        }

        [McpTool("suspension_pause_graph",
            "Pauses the graph of KSP Diag - Suspension, as its button Pause does: it keeps what it shows and leaves " +
            "the new physics steps out; or lets it go on (paused false). With clear, it first forgets every step " +
            "kept. Returns whether it is paused (paused) and how many steps it keeps (steps).")]
        internal static object PauseGraph(bool paused, bool clear = false)
        {
            KSPDiagSuspension mod = Mod();
            if (clear)
            {
                mod.Graph.Clear();
            }
            mod.Graph.Paused = paused;
            return new Dictionary<string, object>
            {
                { "paused", mod.Graph.Paused },
                { "steps", (double)mod.Graph.Steps }
            };
        }

        private static KSPDiagSuspension Mod()
        {
            KSPDiagSuspension mod = UnityEngine.Object.FindObjectOfType<KSPDiagSuspension>();
            if (mod == null)
            {
                throw new InvalidOperationException("KSP Diag - Suspension only runs in flight");
            }
            return mod;
        }
    }
}
