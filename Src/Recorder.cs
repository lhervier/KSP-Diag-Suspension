using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// Writes what the mod reads at each physics step into a CSV file, one line per suspension, the columns of
    /// the vessel repeated on each: from Start to Stop, one file per recording, in the PluginData folder of the
    /// mod. Decimal point, comma separator. The anchor columns (KSP-Fix-SuspensionBounce's static grip, x sideways,
    /// y forward) are empty when the wheel has no anchor at that step.
    /// </summary>
    internal sealed class Recorder
    {
        private const string HEADER =
            "time_s,vessel,speed_mm_s,yaw_rate_deg_s,lateral_m_s,drift_deg,weight_kN,slope_deg,normal_weight_kN,tangential_weight_kN,supported_kN," +
            "supported_pct,cap_sum_kN,tire_force_sum_kN,grip_margin,wheels,grounded,bottomed," +
            "part_id,part,is_grounded,compression,travel_m,is_bottomed,is_extended,spring_kN_m,damper_kN_s_m," +
            "load_kN,friction_load_kN,assumed_load_kN,grip,friction_model,adherent_coef,cap_kN," +
            "tire_side_kN,tire_fwd_kN,tire_kN,used_pct,regime,angular_velocity_rad_s,radius_m,rim_speed_mm_s," +
            "anchor_offset_x_m,anchor_offset_y_m,anchor_force_x_kN,anchor_force_y_kN,anchor_gain_kN_m,anchor_fade," +
            "anchor_reseated";

        private StreamWriter writer;

        internal bool Recording { get { return writer != null; } }

        /// <summary>Path of the file being written, or of the last one written; null before any.</summary>
        internal string FilePath { get; private set; }

        /// <summary>Lines written to the current, or last, file.</summary>
        internal int Lines { get; private set; }

        internal static string Folder
        {
            get
            {
                return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KSPDiagSuspension", "PluginData");
            }
        }

        /// <summary>Starts a new file, closing the one being written if any; returns its path.</summary>
        internal string Start()
        {
            Stop();
            Directory.CreateDirectory(Folder);
            FilePath = Path.GetFullPath(Path.Combine(Folder,
                "suspension-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"));
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            writer.NewLine = "\n";
            writer.WriteLine(HEADER);
            Lines = 0;
            return FilePath;
        }

        /// <summary>Closes the file being written, if any.</summary>
        internal void Stop()
        {
            if (writer != null)
            {
                writer.Close();
                writer = null;
            }
        }

        /// <summary>Writes the lines of one physics step; nothing when not recording.</summary>
        internal void Write(double time, Vessel vessel, SpeedTracker motion, VesselBalance b, List<WheelReading> wheels)
        {
            if (writer == null)
            {
                return;
            }
            string common = string.Join(",", new[]
            {
                F(time, "0.0000"), Q(vessel.vesselName), F(motion.SpeedMmS, "0.00"), F(motion.YawRateDegS, "0.000"),
                F(motion.LateralMS, "0.0000"), F(motion.DriftDeg, "0.00"), F(b.WeightKn), F(b.SlopeDeg, "0.000"),
                F(b.NormalWeightKn), F(b.TangentialWeightKn), F(b.SupportedKn), F(b.SupportedPct, "0.00"),
                F(b.CapKn), F(b.TireForceKn), F(b.GripMargin, "0.000"),
                I(b.Wheels), I(b.Grounded), I(b.Bottomed)
            });
            foreach (WheelReading w in wheels)
            {
                writer.WriteLine(common + "," + string.Join(",", new[]
                {
                    w.Part.flightID.ToString(CultureInfo.InvariantCulture), Q(w.Part.partInfo.title),
                    B(w.Grounded), F(w.Compression, "0.0000"), F(w.TravelM, "0.0000"), B(w.Bottomed), B(w.Extended),
                    F(w.Spring), F(w.Damper), F(w.LoadKn), F(w.FrictionLoadKn), F(w.AssumedLoadKn), F(w.Grip, "0.000"),
                    w.FrictionModel, F(w.AdherentCoef, "0.0000"), F(w.CapKn), F(w.TireForce.x), F(w.TireForce.y),
                    F(w.TireForce.magnitude), F(w.UsedPct, "0.00"), w.Regime, F(w.AngularVelocity, "0.000000"),
                    F(w.RadiusM), F(w.RimSpeedMmS, "0.00"),
                    w.HasAnchor ? F(w.AnchorOffset.x, "0.000000") : "",
                    w.HasAnchor ? F(w.AnchorOffset.y, "0.000000") : "",
                    w.HasAnchor ? F(w.AnchorForce.x, "0.000000") : "",
                    w.HasAnchor ? F(w.AnchorForce.y, "0.000000") : "",
                    w.HasAnchor ? F(w.AnchorGain) : "",
                    w.HasAnchor ? F(w.AnchorFade, "0.0000") : "",
                    w.HasAnchor ? B(w.AnchorReseated) : ""
                }));
                Lines++;
            }
        }

        private static string F(double v, string format = "0.0000")
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string I(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static string B(bool v)
        {
            return v ? "1" : "0";
        }

        private static string Q(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }
    }
}
