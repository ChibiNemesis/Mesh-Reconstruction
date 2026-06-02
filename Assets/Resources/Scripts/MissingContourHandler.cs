using System.Collections.Generic;
using UnityEngine;

public class MissingContourHandler : MonoBehaviour
{
    [Header("Bezier Settings")]
    [SerializeField]
    [Tooltip("How much the interpolated curve bulges outward. 0 = straight line.")]
    private float BezierPower = 0.4f;

    [Header("Debug Gizmos")]
    [SerializeField]
    [Tooltip("Toggle to show the Bezier curves in the Scene view.")]
    private bool ShowBezierGizmos = true;

    [Header("Gizmo Debugging")]
    public bool EnableGizmos = true;
    [Tooltip("Set to a specific Grabber index to unclutter the screen and see just ONE vertex's math. Set to -1 to see all.")]
    public int DebugSpecificVertex = -1;

    // Cache to hold the math data so OnDrawGizmos can draw it safely
    private struct DebugCurveData
    {
        public Vector3 p0, p1, p2, midpoint, centerLocked;
    }
    private List<DebugCurveData> _debugCurves = new List<DebugCurveData>();

    [SerializeField]
    [Tooltip("How smooth the drawn curve is in the Scene view (Number of line segments).")]
    private int GizmoResolution = 15;

    // --- Cached Data for Gizmos ---
    private List<SliceData> cachedSlices;
    private int cachedFirstIndex = -1;
    private int cachedLastIndex = -1;
    private Vector3 cachedFirstCentroid;
    private Vector3 cachedLastCentroid;

    public void HandleMissingContours1(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount, AxisCut axis)
    {
        // Safety checks
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;
        if (FirstContour == null || LastContour == null || FirstContour.sharedMesh == null || LastContour.sharedMesh == null) return;

        // The absolute center of the gap
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            // Safety net: If the list is empty, initialize it using the base transforms 
            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
                for (int j = 0; j < missingSlice.Grabbers.Count; j++)
                {
                    missingSlice.OuterDestinations[j] = missingSlice.Grabbers[j].transform.position;
                }
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // 1. Use the pre-scaled destinations as the source of truth
                Vector3 undeformedPos = missingSlice.OuterDestinations[g];

                // 2. Find P0 and P2 anchored strictly to the patient's actual geometry
                Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
                Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);

                // 3. Calculate the midpoint for the base of the curve
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);
                midpoint = new Vector3(
                    axis == AxisCut.X ? undeformedPos.x : midpoint.x,
                    axis == AxisCut.Y ? undeformedPos.y : midpoint.y,
                    axis == AxisCut.Z ? undeformedPos.z : midpoint.z
                );

                // 4. Calculate P1 (The Control Point)
                // Lock the gap center to the undeformed position's axis to ensure a perfectly flat 2D plane
                Vector3 gapCenterLocked;
                switch (axis)
                {
                    case AxisCut.X: gapCenterLocked = new Vector3(undeformedPos.x, gapCenter.y, gapCenter.z); break;
                    case AxisCut.Y: gapCenterLocked = new Vector3(gapCenter.x, undeformedPos.y, gapCenter.z); break;
                    case AxisCut.Z: default: gapCenterLocked = new Vector3(gapCenter.x, gapCenter.y, undeformedPos.z); break;
                }

                // THE CRITICAL FIX: 
                // We calculate the outward ray using 'undeformedPos', NOT the 'midpoint'.
                // This perfectly preserves the irregular (oval/flat) cross-section of the organ.
                Vector3 outwardDirection = (midpoint - gapCenterLocked).normalized;

                // Apply your manual Bezier power
                //Vector3 p1 = midpoint + (outwardDirection * BezierPower);
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                //Debug Gizmos
                if (EnableGizmos && (DebugSpecificVertex == -1 || g == DebugSpecificVertex))
                {
                    if (i == FirstIndex + 1 && g == 0) _debugCurves.Clear(); // Clear old data on first pass

                    _debugCurves.Add(new DebugCurveData
                    {
                        p0 = p0,
                        p1 = p1,
                        p2 = p2,
                        midpoint = midpoint,
                        centerLocked = gapCenterLocked
                    }); 
                }
                // ----------------

                // 5. Compute the final Bezier coordinate and assign it
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);
                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    public void HandleMissingContours(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount, AxisCut axis)
    {
        // Safety checks
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;
        if (FirstContour == null || LastContour == null || FirstContour.sharedMesh == null || LastContour.sharedMesh == null) return;

        // The absolute center of the gap
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];

            // This single line automatically scales the curve for ANY amount of missing contours
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            // Safety net: Initialize outer destinations if empty
            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
                for (int j = 0; j < missingSlice.Grabbers.Count; j++)
                {
                    missingSlice.OuterDestinations[j] = missingSlice.Grabbers[j].transform.position;
                }
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // 1. Use the pre-scaled destinations as the source of truth
                Vector3 undeformedPos = missingSlice.OuterDestinations[g];

                // 2. Find P0 and P2 anchored strictly to the patient's actual geometry
                Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
                Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);

                // 3. Calculate the true mathematical midpoint
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // Clamp the midpoint to the exact mathematical 2D plane of the slice
                midpoint = new Vector3(
                    axis == AxisCut.X ? undeformedPos.x : midpoint.x,
                    axis == AxisCut.Y ? undeformedPos.y : midpoint.y,
                    axis == AxisCut.Z ? undeformedPos.z : midpoint.z
                );

                // 4. Lock the gap center to the undeformed position's axis to ensure a perfectly flat 2D outward push
                Vector3 gapCenterLocked;
                switch (axis)
                {
                    case AxisCut.X: gapCenterLocked = new Vector3(undeformedPos.x, gapCenter.y, gapCenter.z); break;
                    case AxisCut.Y: gapCenterLocked = new Vector3(gapCenter.x, undeformedPos.y, gapCenter.z); break;
                    case AxisCut.Z: default: gapCenterLocked = new Vector3(gapCenter.x, gapCenter.y, undeformedPos.z); break;
                }

                // Calculate the outward ray strictly using the clamped midpoint
                Vector3 outwardDirection = (midpoint - gapCenterLocked).normalized;

                // Apply your manual Bezier power to calculate P1
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                // Optional: Debug Gizmos
                if (EnableGizmos && (DebugSpecificVertex == -1 || g == DebugSpecificVertex))
                {
                    if (i == FirstIndex + 1 && g == 0) _debugCurves.Clear();
                    _debugCurves.Add(new DebugCurveData
                    {
                        p0 = p0,
                        p1 = p1,
                        p2 = p2,
                        midpoint = midpoint,
                        centerLocked = gapCenterLocked
                    });
                }

                // 5. Compute the final Bezier coordinate and assign it
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);
                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    /// <summary>
    /// Mathematically finds the absolute closest vertex on the target mesh collider.
    /// Bypasses Unity's physics engine limitations with non-convex anatomies.
    /// </summary>
    private Vector3 GetClosestVertexOnMesh(Vector3 targetPoint, MeshCollider collider)
    {
        Transform meshTransform = collider.transform;
        Mesh mesh = collider.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        float minDistanceSqr = float.MaxValue;
        Vector3 closestWorldVertex = targetPoint;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldVertex = meshTransform.TransformPoint(vertices[i]);
            float distSqr = (targetPoint - worldVertex).sqrMagnitude;

            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                closestWorldVertex = worldVertex;
            }
        }
        return closestWorldVertex;
    }

    /// <summary>
    /// Computes the point on a quadratic bezier curve at parameter t.
    /// </summary>
    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 p = uu * p0;
        p += 2 * u * t * p1;
        p += tt * p2;

        return p;
    }

    // =========================================================
    // VISUAL DEBUGGING: GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (!EnableGizmos || _debugCurves == null || _debugCurves.Count == 0) return;

        foreach (var curve in _debugCurves)
        {
            // 1. Draw the Base Points
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(curve.p0, 0.002f); // Bottom Patient Contour
            Gizmos.DrawWireSphere(curve.p2, 0.002f); // Top Patient Contour

            // 2. Draw the Midpoint and Center
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(curve.midpoint, 0.002f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(curve.centerLocked, 0.002f);

            // 3. Draw the Outward Direction Ray (The likely culprit!)
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(curve.centerLocked, curve.midpoint);
            Gizmos.DrawLine(curve.midpoint, curve.p1); // The outward push

            // Draw Control Point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(curve.p1, 0.002f);

            // 4. Draw the actual Bezier Curve
            Gizmos.color = Color.white;
            Vector3 previousPoint = curve.p0;
            for (int i = 1; i <= GizmoResolution; i++)
            {
                float t = i / (float)GizmoResolution;
                Vector3 nextPoint = CalculateQuadraticBezierPoint(t, curve.p0, curve.p1, curve.p2);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}