using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.github.lhervier.ksp.diag.suspension
{
    /// <summary>
    /// Draws the static-grip anchors of KSP-Fix-SuspensionBounce under the wheels on the ground: a cross at the anchor
    /// point, a line from the contact to it, and an arrow from the contact for the force the anchor adds to the tyre,
    /// <see cref="FORCE_SCALE"/> metres per kN. Colour: green while it holds in full, orange while it fades, grey while
    /// off, red for <see cref="RESEAT_HOLD"/> seconds of game time after a step it was moved. Lines of one pixel, seen
    /// through everything. Rebuilt every frame from the part transforms, so the floating origin cannot shift it.
    /// </summary>
    internal sealed class AnchorDrawing
    {
        private const string LOG_PREFIX = "[KSPDiagSuspension] ";

        // Unity's own shader for its debug lines: unlike the unlit shaders of the game, it lets a script choose the
        // depth test (the same as KSP-Diag-PartFlex).
        private const string SHADER_NAME = "Hidden/Internal-Colored";

        // Layer the drawing is queued on: Local Scenery, which the flight cameras draw.
        private const int DRAW_LAYER = 15;

        // Drawn after everything else.
        private const int LINE_QUEUE = 4002;

        /// <summary>Length of the force arrow per kN of anchor force, in metres: 0.3 kN draws 0.6 m.</summary>
        internal const float FORCE_SCALE = 2f;

        // Half the size of the cross at the anchor point, and of the arrow heads, in metres.
        private const float CROSS = 0.05f;
        private const float HEAD = 0.04f;

        /// <summary>How long a wheel stays red after its anchor was moved, in seconds of game time: one step alone
        /// would hardly show.</summary>
        internal const double RESEAT_HOLD = 0.2;

        private static readonly Color HOLDS = new Color(0.1f, 1f, 0.1f, 1f);
        private static readonly Color FADING = new Color(1f, 0.55f, 0f, 1f);
        private static readonly Color OFF = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color RESEAT = new Color(1f, 0.1f, 0.1f, 1f);

        private readonly Material material;
        private readonly Mesh mesh;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();

        // Game time of the last reseat seen, per part flight id.
        private readonly Dictionary<uint, double> lastReseat = new Dictionary<uint, double>();

        /// <summary>Sets the drawing up; it draws nothing if Unity's line shader is missing (said in the log).</summary>
        internal AnchorDrawing()
        {
            Shader shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError(LOG_PREFIX + "shader " + SHADER_NAME + " not found: anchors will not be drawn");
                return;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // White: the lines carry their colour in their vertices.
            material.SetColor("_Color", Color.white);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.renderQueue = LINE_QUEUE;
            mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
        }

        /// <summary>Notes the reseats of a physics step, so that they stay red for <see cref="RESEAT_HOLD"/>.</summary>
        internal void NoteStep(double time, List<WheelReading> wheels)
        {
            foreach (WheelReading w in wheels)
            {
                if (w.HasAnchor && w.AnchorReseated)
                {
                    lastReseat[w.Part.flightID] = time;
                }
            }
        }

        /// <summary>Draws the anchors of the wheels on the ground for this frame; call once per frame.</summary>
        internal void Draw(double time, List<WheelReading> wheels)
        {
            if (material == null)
            {
                return;
            }
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            foreach (WheelReading w in wheels)
            {
                if (!w.Grounded || !w.HasAnchor || w.Part == null || w.Part.transform == null)
                {
                    continue;
                }
                Transform frame = w.Part.transform;
                Vector3 contact = frame.TransformPoint(w.ContactLocal);
                Vector3 side = frame.TransformDirection(w.SidewaysLocal);
                Vector3 fwd = frame.TransformDirection(w.ForwardLocal);
                Vector3 anchor = contact - side * w.AnchorOffset.x - fwd * w.AnchorOffset.y;
                Vector3 force = (side * w.AnchorForce.x + fwd * w.AnchorForce.y) * FORCE_SCALE;

                double reseat;
                Color color = lastReseat.TryGetValue(w.Part.flightID, out reseat) && time - reseat < RESEAT_HOLD
                    ? RESEAT
                    : w.AnchorGain <= 0f ? OFF
                    : w.AnchorFade >= 1f ? HOLDS
                    : FADING;

                Line(anchor - side * CROSS, anchor + side * CROSS, color);
                Line(anchor - fwd * CROSS, anchor + fwd * CROSS, color);
                Line(contact, anchor, color);
                if (force.sqrMagnitude > 1e-8f)
                {
                    Vector3 tip = contact + force;
                    Vector3 back = force.normalized * HEAD;
                    Vector3 across = Vector3.Cross(back, frame.TransformDirection(Vector3.Cross(w.SidewaysLocal,
                        w.ForwardLocal))).normalized * HEAD * 0.5f;
                    Line(contact, tip, color);
                    Line(tip, tip - back + across, color);
                    Line(tip, tip - back - across, color);
                }
            }
            mesh.Clear();
            if (indices.Count == 0)
            {
                return;
            }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, DRAW_LAYER);
        }

        /// <summary>Releases the material and the mesh.</summary>
        internal void Destroy()
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }
            if (material != null)
            {
                Object.Destroy(material);
            }
        }

        private void Line(Vector3 a, Vector3 b, Color color)
        {
            indices.Add(vertices.Count);
            vertices.Add(a);
            colors.Add(color);
            indices.Add(vertices.Count);
            vertices.Add(b);
            colors.Add(color);
        }
    }
}
