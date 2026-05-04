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

    [SerializeField]
    [Tooltip("How smooth the drawn curve is in the Scene view (Number of line segments).")]
    private int GizmoResolution = 15;

    // --- Cached Data for Gizmos ---
    private List<SliceData> cachedSlices;
    private int cachedFirstIndex = -1;
    private int cachedLastIndex = -1;
    private Vector3 cachedFirstCentroid;
    private Vector3 cachedLastCentroid;

    /// <summary>
    /// Handles the missing contours between two found contours.
    /// </summary>
    public void HandleMissingContours(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;

        cachedSlices = SliceGrabbers;
        cachedFirstIndex = FirstIndex;
        cachedLastIndex = LastIndex;
        cachedFirstCentroid = FirstCentroid;
        cachedLastCentroid = LastCentroid;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            // The centroid of the organ at this specific missing height
            Vector3 currentAxisCenter = Vector3.Lerp(FirstCentroid, LastCentroid, t);

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                Vector3 undeformedPos = missingSlice.Grabbers[g].transform.position;

                // CRITICAL FIX 1 & 2: Calculate the exact radial direction of this specific vertex
                Vector3 localRadialDir = (undeformedPos - currentAxisCenter).normalized;

                // ---------------------------------------------------------
                // 1. FIND MATCHING P0 (Bottom Slice)
                // ---------------------------------------------------------
                int bestBottomIndex = 0;
                float maxBottomDot = -2f;

                for (int b = 0; b < bottomSlice.Grabbers.Count; b++)
                {
                    Vector3 bottomDir = (bottomSlice.Grabbers[b].transform.position - FirstCentroid).normalized;
                    float dot = Vector3.Dot(localRadialDir, bottomDir);
                    if (dot > maxBottomDot)
                    {
                        maxBottomDot = dot;
                        bestBottomIndex = b;
                    }
                }

                // Get the literal position of the matched bottom Grabber
                Vector3 bottomMatchPos = bottomSlice.OuterDestinations != null && bottomSlice.OuterDestinations.Count > bestBottomIndex
                    ? bottomSlice.OuterDestinations[bestBottomIndex]
                    : bottomSlice.Grabbers[bestBottomIndex].transform.position;

                // CRITICAL PINCH-POINT FIX: Measure the radius, then apply it to our unique ray
                float bottomRadius = Vector3.Distance(FirstCentroid, bottomMatchPos);
                Vector3 p0 = FirstCentroid + (localRadialDir * bottomRadius);


                // ---------------------------------------------------------
                // 2. FIND MATCHING P2 (Top Slice)
                // ---------------------------------------------------------
                int bestTopIndex = 0;
                float maxTopDot = -2f;

                for (int top = 0; top < topSlice.Grabbers.Count; top++)
                {
                    Vector3 topDir = (topSlice.Grabbers[top].transform.position - LastCentroid).normalized;
                    float dot = Vector3.Dot(localRadialDir, topDir);
                    if (dot > maxTopDot)
                    {
                        maxTopDot = dot;
                        bestTopIndex = top;
                    }
                }

                // Get the literal position of the matched top Grabber
                Vector3 topMatchPos = topSlice.OuterDestinations != null && topSlice.OuterDestinations.Count > bestTopIndex
                    ? topSlice.OuterDestinations[bestTopIndex]
                    : topSlice.Grabbers[bestTopIndex].transform.position;

                // CRITICAL PINCH-POINT FIX: Measure the radius, then apply it to our unique ray
                float topRadius = Vector3.Distance(LastCentroid, topMatchPos);
                Vector3 p2 = LastCentroid + (localRadialDir * topRadius);


                // ---------------------------------------------------------
                // 3. CALCULATE BEZIER CURVE (Using Local Ray Preservation)
                // ---------------------------------------------------------
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // CRITICAL FIX 2: Push outward along the specific vertex's ray, not a generalized center ray.
                // This prevents vertices in dense clusters from crossing over each other and tearing holes!
                Vector3 p1 = midpoint + (localRadialDir * BezierPower);

                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
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
        if (!ShowBezierGizmos || cachedSlices == null || cachedFirstIndex < 0 || cachedLastIndex >= cachedSlices.Count) return;

        SliceData bottomSlice = cachedSlices[cachedFirstIndex];
        SliceData topSlice = cachedSlices[cachedLastIndex];

        if (bottomSlice.Grabbers == null || topSlice.Grabbers == null) return;

        Vector3 gapCenter = Vector3.Lerp(cachedFirstCentroid, cachedLastCentroid, 0.5f);

        Gizmos.color = Color.cyan;

        for (int g = 0; g < bottomSlice.Grabbers.Count; g++)
        {
            if (g >= topSlice.Grabbers.Count) break;

            // CRITICAL FIX: Draw the debug lines using the destinations
            Vector3 p0 = bottomSlice.OuterDestinations[g];
            Vector3 p2 = topSlice.OuterDestinations[g];

            Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);
            Vector3 outwardDirection = (midpoint - gapCenter).normalized;
            Vector3 p1 = midpoint + (outwardDirection * BezierPower);

            Vector3 previousPoint = p0;

            for (int i = 1; i <= GizmoResolution; i++)
            {
                float t = i / (float)GizmoResolution;
                Vector3 nextPoint = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }
    }
}