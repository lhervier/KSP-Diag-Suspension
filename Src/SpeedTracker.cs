using System.Collections.Generic;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// Speed of a vessel over the ground, precise to a hundredth of mm/s: how far its root part moved in the frame
    /// of the body (latitude, longitude, altitude, in double), over the last second of physics; how fast it turns
    /// around the local vertical; and how much it moves sideways, against the heading the navball shows. Shown and
    /// recorded only: nothing else in this mod uses them.
    /// </summary>
    internal sealed class SpeedTracker
    {
        // Length of the window the speed is averaged over, in seconds of game time.
        internal const double WINDOW = 1.0;

        private struct Sample
        {
            internal double Time;
            internal Vector3d Position;
        }

        private readonly Queue<Sample> samples = new Queue<Sample>();
        private Vessel vessel;

        /// <summary>Speed in mm/s over the window; NaN until a full window was seen.</summary>
        internal double SpeedMmS { get; private set; } = double.NaN;

        /// <summary>
        /// How fast the vessel turns around the local vertical at this step, in degrees per second, positive when its
        /// heading grows (to the right, seen from above); NaN without a rigidbody.
        /// </summary>
        internal double YawRateDegS { get; private set; } = double.NaN;

        /// <summary>
        /// Speed over the ground across the heading at this step, in m/s, positive to the right. The heading is the
        /// navball's: the up axis of the part the vessel is controlled from, laid flat.
        /// </summary>
        internal double LateralMS { get; private set; } = double.NaN;

        /// <summary>
        /// Angle between the heading and the horizontal speed over the ground at this step, in degrees, positive when
        /// the vessel moves to the right of where it points; NaN below <see cref="DRIFT_MIN_SPEED"/>.
        /// </summary>
        internal double DriftDeg { get; private set; } = double.NaN;

        // Speed under which the drift angle is not given, in m/s: at rest, its direction is noise.
        internal const double DRIFT_MIN_SPEED = 0.5;

        /// <summary>Adds the position of the vessel at a physics step; starts over for another vessel or body.</summary>
        internal void Add(Vessel v, double time)
        {
            if (v != vessel || v.rootPart == null)
            {
                vessel = v;
                samples.Clear();
                SpeedMmS = double.NaN;
                YawRateDegS = double.NaN;
                LateralMS = double.NaN;
                DriftDeg = double.NaN;
                if (v == null || v.rootPart == null)
                {
                    return;
                }
            }
            CelestialBody body = v.mainBody;
            Vector3d world = v.rootPart.transform.position;
            // Near the ground, the frame of the physics turns with the body: the angular velocity of the root part's
            // rigidbody is its turn over the ground. Unity's frame is left-handed: a positive turn around the local
            // vertical goes to the right, seen from above (checked: +90 over a right turn from north to east).
            UnityEngine.Rigidbody rb = v.rootPart.rb;
            YawRateDegS = rb != null
                ? UnityEngine.Vector3.Dot(rb.angularVelocity, (UnityEngine.Vector3)FlightGlobals.getUpAxis(body, world))
                    * UtilMath.Rad2Deg
                : double.NaN;
            Drift(v, (Vector3d)FlightGlobals.getUpAxis(body, world));
            double lat = body.GetLatitude(world) * UtilMath.Deg2Rad;
            double lon = body.GetLongitude(world) * UtilMath.Deg2Rad;
            double r = body.Radius + body.GetAltitude(world);
            Vector3d p = new Vector3d(
                r * System.Math.Cos(lat) * System.Math.Cos(lon),
                r * System.Math.Cos(lat) * System.Math.Sin(lon),
                r * System.Math.Sin(lat));
            samples.Enqueue(new Sample { Time = time, Position = p });

            // Keep the oldest sample at least a window old, no older than needed.
            while (samples.Count > 2)
            {
                Sample[] two = TwoOldest();
                if (time - two[1].Time >= WINDOW)
                {
                    samples.Dequeue();
                }
                else
                {
                    break;
                }
            }
            Sample oldest = samples.Peek();
            double dt = time - oldest.Time;
            SpeedMmS = dt >= WINDOW ? 1000.0 * (p - oldest.Position).magnitude / dt : double.NaN;
        }

        internal void Clear()
        {
            vessel = null;
            samples.Clear();
            SpeedMmS = double.NaN;
            YawRateDegS = double.NaN;
            LateralMS = double.NaN;
            DriftDeg = double.NaN;
        }

        /// <summary>Sets the lateral speed and the drift angle of the vessel against the heading.</summary>
        private void Drift(Vessel v, Vector3d up)
        {
            UnityEngine.Transform reference = v.ReferenceTransform;
            if (reference == null)
            {
                LateralMS = DriftDeg = double.NaN;
                return;
            }
            Vector3d forward = Vector3d.Exclude(up, (Vector3d)reference.up);
            Vector3d velocity = Vector3d.Exclude(up, v.srf_velocity);
            if (forward.sqrMagnitude < 1e-9)
            {
                LateralMS = DriftDeg = double.NaN;
                return;
            }
            forward = forward.normalized;
            // Unity's frame is left-handed: up × forward points to the right.
            Vector3d right = Vector3d.Cross(up, forward);
            double along = Vector3d.Dot(velocity, forward);
            LateralMS = Vector3d.Dot(velocity, right);
            DriftDeg = velocity.magnitude >= DRIFT_MIN_SPEED
                ? System.Math.Atan2(LateralMS, along) * UtilMath.Rad2Deg
                : double.NaN;
        }

        private Sample[] TwoOldest()
        {
            Sample[] two = new Sample[2];
            int i = 0;
            foreach (Sample s in samples)
            {
                two[i++] = s;
                if (i == 2)
                {
                    break;
                }
            }
            return two;
        }
    }
}
