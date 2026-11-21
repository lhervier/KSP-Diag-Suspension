using ModuleWheels;
using UnityEngine;
using VehiclePhysics;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// What the game computed for one suspension during the last physics step, read as is. Forces in kN, the
    /// unit of the game (tonnes times m/s²); lengths in metres. Nothing here uses a speed.
    /// </summary>
    internal sealed class WheelReading
    {
        // Compression from which a suspension is said bottomed out, and up to which it is said fully extended
        // while touching the ground.
        internal const float BOTTOMED = 0.99f;
        internal const float EXTENDED = 0.01f;

        internal Part Part;
        internal bool Grounded;

        /// <summary>Contact depth over the travel: 0 fully extended, 1 bottomed out (it may go beyond). Measured
        /// from where the wheel really sits, its collider's centre offset included (<see cref="TrueCompression"/>).</summary>
        internal float Compression;
        internal float TravelM;

        /// <summary>Spring rate (kN/m) and damper rate (kN·s/m) the suspension module gave the wheel.</summary>
        internal float Spring;
        internal float Damper;

        /// <summary>Force of the suspension on the ground, spring and damper: WheelHit.force.</summary>
        internal float LoadKn;

        /// <summary>Load the tyre model works with: the suspension force plus the extra downforce, floored by
        /// the minimum downforce of the wheel.</summary>
        internal float FrictionLoadKn;

        /// <summary>Load the suspension module sized the spring for (sprungMassGravity, in tonnes at 1 g, the
        /// 0.5 t floor of the load balancer included), in kN; NaN without a suspension module.</summary>
        internal float AssumedLoadKn;

        internal float Grip;

        /// <summary>Friction coefficient of the adherent part of the tyre curve, every multiplier included.</summary>
        internal float AdherentCoef;

        /// <summary>Largest force the tyre gives while it holds (adherent regime): friction load × grip ×
        /// adherent coefficient.</summary>
        internal float CapKn;

        /// <summary>Tyre force the game applied, x sideways, y forward (smoothed by the game).</summary>
        internal Vector2 TireForce;

        internal string FrictionModel;

        /// <summary>Rotation of the wheel on its axle, rad/s, as the tyre model integrates it; and its radius.</summary>
        internal float AngularVelocity;
        internal float RadiusM;

        /// <summary>Speed of the tread around the axle, mm/s (rotation × radius): what the rover would move at if
        /// the wheel rolled without slipping. Not the speed of the vessel.</summary>
        internal float RimSpeedMmS { get { return 1000f * Mathf.Abs(AngularVelocity) * RadiusM; } }

        // World directions, for the vessel balance.
        internal Vector3 Normal;
        internal Vector3 SuspensionAxis;
        internal Vector3 TireForceWorld;

        // The static-grip anchor of KSP-Fix-WheelCreep, when that mod is installed (AnchorReader): the way the
        // contact travelled from the anchor point (m) and the force the anchor added to the tyre (kN), x sideways,
        // y forward; force per metre of offset (kN/m, 0 while off); how much it held (1 in full, 0 let go); whether it
        // was moved at this step because the tyre did not pass its force on.
        internal bool HasAnchor;
        internal Vector2 AnchorOffset;
        internal Vector2 AnchorForce;
        internal float AnchorGain;
        internal float AnchorFade;
        internal bool AnchorReseated;

        // Contact point and tyre axes in the frame of the part, for drawing the anchor wherever the floating origin
        // has moved the part since.
        internal Vector3 ContactLocal;
        internal Vector3 SidewaysLocal;
        internal Vector3 ForwardLocal;

        /// <summary>The anchor's state: "-" without one, "reseat" at a step it was moved, "off" while its gain is 0,
        /// "holds" at full fade, "fading" in between.</summary>
        internal string AnchorState
        {
            get
            {
                if (!HasAnchor)
                {
                    return "-";
                }
                if (AnchorReseated)
                {
                    return "reseat";
                }
                if (AnchorGain <= 0f)
                {
                    return "off";
                }
                return AnchorFade >= 1f ? "holds" : "fading";
            }
        }

        internal bool Bottomed { get { return Grounded && Compression >= BOTTOMED; } }

        internal bool Extended { get { return Grounded && Compression <= EXTENDED; } }

        /// <summary>Share of the adherent cap the tyre force uses, in %; NaN without a cap.</summary>
        internal float UsedPct { get { return CapKn > 0f ? 100f * TireForce.magnitude / CapKn : float.NaN; } }

        /// <summary>The tyre force against the adherent cap: "air" off the ground, "holds" up to the cap,
        /// "skids fwd" or "skids side" beyond it, after the larger component of the tyre force.</summary>
        internal string Regime
        {
            get
            {
                if (!Grounded)
                {
                    return "air";
                }
                if (!(UsedPct > 100f))
                {
                    return "holds";
                }
                return Mathf.Abs(TireForce.x) > Mathf.Abs(TireForce.y) ? "skids side" : "skids fwd";
            }
        }

        /// <summary>
        /// Compression of a grounded wheel from the contact depth of <paramref name="state"/>, measured from where
        /// the wheel really is: 0 fully extended, 1 on its end-stop.
        /// </summary>
        private static float TrueCompression(VPWheelCollider collider, VehicleBase.WheelState state)
        {
            float travel = collider.lastRuntimeSuspensionTravel;
            if (!(travel > 0f))
            {
                return state.suspensionCompression;
            }
            // The game places PhysX's wheel at the collider's centre offset, shifted along the travel by the
            // suspension anchor, but its contact depth (and the compression it derives from it) counts the anchor's
            // shift only. A part that sets a centre offset (the LT-1 leg, 0.119 m below its transform) would read
            // that much less compressed: short of its end-stop while it rests on it. The depth is measured along the
            // collider's up axis, where only the offset's y counts.
            return (state.contactDepth - collider.center.y) / travel;
        }

        /// <summary>Reads a wheel; null when its wheel controller is not set up yet.</summary>
        internal static WheelReading Read(ModuleWheelBase wheelBase)
        {
            KSPWheelController controller = wheelBase.Wheel;
            if (controller == null || controller.wheelCollider == null)
            {
                return null;
            }
            VehicleBase.WheelState state = controller.currentState;
            if (state == null)
            {
                return null;
            }
            VPWheelCollider collider = controller.wheelCollider;
            TireFriction friction = controller.tireFriction;

            WheelReading r = new WheelReading();
            r.Part = wheelBase.part;
            r.Grounded = state.grounded;
            r.Compression = r.Grounded ? TrueCompression(collider, state) : 0f;
            r.TravelM = collider.runtimeSuspensionTravel;
            r.Spring = collider.springRate;
            r.Damper = collider.damperRate;
            r.LoadKn = r.Grounded ? state.hit.force : 0f;
            r.FrictionLoadKn = r.Grounded ? state.downforce : 0f;

            ModuleWheelSuspension suspension = wheelBase.part.FindModuleImplementing<ModuleWheelSuspension>();
            r.AssumedLoadKn = suspension != null
                ? suspension.sprungMassGravity * (float)PhysicsGlobals.GravitationalAcceleration
                : float.NaN;

            r.Grip = state.groundMaterial != null ? state.groundMaterial.grip : 1f;
            r.FrictionModel = friction != null ? friction.model.ToString() : "none";
            r.AdherentCoef = friction != null ? friction.settings.adherent.y * friction.frictionMultiplier : 0f;
            r.CapKn = r.FrictionLoadKn * r.Grip * r.AdherentCoef;
            r.TireForce = r.Grounded ? state.tireForce : Vector2.zero;
            r.AngularVelocity = state.angularVelocity;
            r.RadiusM = controller.WheelRadius;

            r.SuspensionAxis = controller.SuspensionAxis;
            if (r.Grounded)
            {
                r.Normal = state.hit.normal;
                r.TireForceWorld = state.hit.forwardDir * r.TireForce.y + state.hit.sidewaysDir * r.TireForce.x;
                Transform frame = wheelBase.part.transform;
                r.ContactLocal = frame.InverseTransformPoint(state.hit.point);
                r.SidewaysLocal = frame.InverseTransformDirection(state.hit.sidewaysDir);
                r.ForwardLocal = frame.InverseTransformDirection(state.hit.forwardDir);
            }
            AnchorReader.Read(state, r);
            return r;
        }
    }
}
