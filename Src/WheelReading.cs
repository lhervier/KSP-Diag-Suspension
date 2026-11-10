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

        /// <summary>Contact depth over the travel: 0 fully extended, 1 bottomed out (it may go beyond).</summary>
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
            r.Compression = r.Grounded ? state.suspensionCompression : 0f;
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
            }
            return r;
        }
    }
}
