using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// The balance of the forces between a vessel and the ground, from its weight and what its suspensions read:
    /// how much of the weight pressing it on the ground they carry, and how much tyre grip they have against the
    /// part of the weight pulling it down the slope. Forces in kN. Only gravity counts as weight: at rest that
    /// is the whole story; moving or turning, the inertial forces are left out. Nothing here uses a speed.
    /// </summary>
    internal sealed class VesselBalance
    {
        internal int Wheels;
        internal int Grounded;
        internal int Bottomed;

        /// <summary>Weight of the vessel (total mass × local gravity).</summary>
        internal double WeightKn;

        /// <summary>Angle between the mean ground normal under the wheels and the local vertical, in degrees.</summary>
        internal double SlopeDeg;

        /// <summary>Part of the weight along the ground normal, pressing on the ground.</summary>
        internal double NormalWeightKn;

        /// <summary>Part of the weight along the ground, pulling down the slope.</summary>
        internal double TangentialWeightKn;

        /// <summary>Sum of the suspension forces along the ground normal.</summary>
        internal double SupportedKn;

        /// <summary>Sum of the adherent caps of the tyres touching the ground.</summary>
        internal double CapKn;

        /// <summary>Sum of the tyre forces the game applied, as a vector, its size.</summary>
        internal double TireForceKn;

        /// <summary>Share of the normal weight the suspensions carry, in %; NaN with no wheel on the ground.</summary>
        internal double SupportedPct { get { return NormalWeightKn > 0 ? 100.0 * SupportedKn / NormalWeightKn : double.NaN; } }

        /// <summary>How many times the tyre grip covers the pull down the slope; NaN on flat ground.</summary>
        internal double GripMargin { get { return TangentialWeightKn > 1e-6 ? CapKn / TangentialWeightKn : double.NaN; } }

        internal static VesselBalance Compute(Vessel vessel, List<WheelReading> wheels)
        {
            VesselBalance b = new VesselBalance();
            b.Wheels = wheels.Count;
            Vector3d gravity = FlightGlobals.getGeeForceAtPosition(vessel.CoM);
            Vector3d weight = gravity * vessel.totalMass;
            b.WeightKn = weight.magnitude;

            Vector3d normalSum = Vector3d.zero;
            Vector3d tireSum = Vector3d.zero;
            foreach (WheelReading w in wheels)
            {
                if (!w.Grounded)
                {
                    continue;
                }
                b.Grounded++;
                if (w.Bottomed)
                {
                    b.Bottomed++;
                }
                normalSum += (Vector3d)w.Normal;
                tireSum += (Vector3d)w.TireForceWorld;
                b.CapKn += w.CapKn;
            }
            b.TireForceKn = tireSum.magnitude;
            if (b.Grounded == 0 || normalSum.sqrMagnitude < 1e-12)
            {
                b.SlopeDeg = double.NaN;
                return b;
            }
            Vector3d n = normalSum.normalized;
            b.SlopeDeg = Vector3d.Angle(n, -gravity);
            double pressing = -Vector3d.Dot(weight, n);
            b.NormalWeightKn = pressing;
            b.TangentialWeightKn = (weight + n * pressing).magnitude;
            foreach (WheelReading w in wheels)
            {
                if (w.Grounded)
                {
                    b.SupportedKn += w.LoadKn * Mathf.Abs(Vector3.Dot(w.SuspensionAxis, (Vector3)n));
                }
            }
            return b;
        }
    }
}
