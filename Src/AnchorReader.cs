using System;
using System.Reflection;
using UnityEngine;
using VehiclePhysics;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// Reads the static-grip anchors of KSP-Fix-SuspensionBounce (TyreStaticGrip.TryGetAnchor) by reflection, when that
    /// mod is installed: this mod does not depend on it. Without it, or with a version that has no such method, no
    /// wheel has an anchor, and that is said once in the log.
    /// </summary>
    internal static class AnchorReader
    {
        private const string LOG_PREFIX = "[KSPDiagSuspension] ";
        private const string TYPE_NAME = "com.github.lhervier.ksp.fix.suspensionbounce.TyreStaticGrip";
        private const string METHOD_NAME = "TryGetAnchor";

        private delegate bool TryGetAnchorFn(VehicleBase.WheelState wheel, out Vector2 offset, out Vector2 force,
            out float gain, out float fade, out bool reseated);

        private static bool resolved;
        private static TryGetAnchorFn tryGetAnchor;

        /// <summary>Whether KSP-Fix-SuspensionBounce, with its anchor accessor, is loaded.</summary>
        internal static bool Available
        {
            get
            {
                Resolve();
                return tryGetAnchor != null;
            }
        }

        /// <summary>
        /// Reads the anchor of a wheel at the current physics step into <paramref name="r"/> (HasAnchor and the Anchor
        /// fields); HasAnchor false when the fix is absent, off, or has no anchor for that wheel at this step.
        /// </summary>
        internal static void Read(VehicleBase.WheelState wheel, WheelReading r)
        {
            r.HasAnchor = false;
            if (!Available)
            {
                return;
            }
            Vector2 offset, force;
            float gain, fade;
            bool reseated;
            try
            {
                r.HasAnchor = tryGetAnchor(wheel, out offset, out force, out gain, out fade, out reseated);
            }
            catch (Exception e)
            {
                // Never again: one error in the log, not one per wheel and per step.
                Debug.LogError(LOG_PREFIX + "reading the anchors of KSP-Fix-SuspensionBounce failed, stopped: " + e);
                tryGetAnchor = null;
                return;
            }
            r.AnchorOffset = offset;
            r.AnchorForce = force;
            r.AnchorGain = gain;
            r.AnchorFade = fade;
            r.AnchorReseated = reseated;
        }

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(TYPE_NAME, false);
                }
                catch (Exception)
                {
                    continue;
                }
                if (type == null)
                {
                    continue;
                }
                MethodInfo method = type.GetMethod(METHOD_NAME, BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    tryGetAnchor = (TryGetAnchorFn)Delegate.CreateDelegate(typeof(TryGetAnchorFn), method, false);
                }
                break;
            }
            Debug.Log(LOG_PREFIX + (tryGetAnchor != null
                ? "KSP-Fix-SuspensionBounce found: its static-grip anchors are read"
                : "no KSP-Fix-SuspensionBounce anchor accessor: anchors are not shown"));
        }
    }
}
