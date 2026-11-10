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
            "Reads what the window of KSP Diag - Suspension shows for the active vessel, as computed by the game " +
                "at the last physics step (forces in kN). Vessel: name (vessel), game time of the reading (time), " +
                "speed over the ground in mm/s averaged over the last second (speedMmS, shown only, used for " +
                "nothing else), how fast it turns around the local vertical in degrees per second at that step, " +
                "positive to the right seen from above (yawRateDegS), its speed over the ground across the heading the " +
                "navball shows in m/s, positive to the right (lateralMS), and the angle between that heading and " +
                "its horizontal speed in degrees, positive when it moves to the right of where it points, none " +
                "under 0.5 m/s (driftDeg) (all three shown only too), weight (weightKn), angle " +
                "of the mean ground normal under the wheels to the vertical (slopeDeg), the part of the weight pressing on the ground (normalWeightKn) and pulling " +
                "down the slope (tangentialWeightKn), the sum of the suspension forces along the ground normal " +
                "(supportedKn) and its share of normalWeightKn (supportedPct), the sum of the adherent tyre caps " +
                "(capKn), the size of the sum of the tyre forces applied (tireForceKn), capKn over " +
                "tangentialWeightKn (gripMargin), how many suspensions touch the ground (grounded) and are " +
                "bottomed out (bottomed); whether a CSV is being recorded (recording), its path (csv) and lines " +
                "(csvLines). Per suspension (wheels): part title, name and flight id (part, partName, partId), " +
                "whether it touches the ground (grounded), contact depth over the travel (compression, 0 fully " +
                "extended, 1 bottomed out), the travel in m (travelM), bottomed (compression >= 0.99) and " +
                "extended (<= 0.01, on the ground), spring in kN/m and damper in kN.s/m (spring, damper), the " +
                "suspension force (loadKn), the load of the tyre model (frictionLoadKn: plus extra downforce, " +
                "floored), the load the spring was sized for (assumedLoadKn, sprungMassGravity, 0.5 t floor " +
                "included), ground grip (grip), friction model, adherent coefficient (adherentCoef, multipliers " +
                "included), largest tyre force while it holds (capKn = frictionLoadKn x grip x adherentCoef), the " +
                "tyre force applied sideways and forward (tireSideKn, tireFwdKn) and its size over capKn in % " +
                "(usedPct), the regime (regime: air, holds up to the cap, skids fwd or skids side beyond it after " +
                "the larger component), the rotation of the wheel on its axle in rad/s (angularVelocity), its " +
                "radius in m (radiusM) and the speed of its tread around the axle in mm/s (rimSpeedMmS: what the " +
                "vessel would move at if the wheel rolled without slipping).")]
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
