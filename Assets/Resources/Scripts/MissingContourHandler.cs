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

        // Cache the data so OnDrawGizmos can see it
        cachedSlices = SliceGrabbers;
        cachedFirstIndex = FirstIndex;
        cachedLastIndex = LastIndex;
        cachedFirstCentroid = FirstCentroid;
        cachedLastCentroid = LastCentroid;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];

        // Loop through every missing slice in the gap
        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            Vector3 currentAxisCenter = Vector3.Lerp(FirstCentroid, LastCentroid, t);

            // Ensure the missing slice has an OuterDestinations list we can write into
            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // Reference point is the grabber's position (best available anchor in the missing slice)
                Vector3 referencePos = missingSlice.Grabbers[g].transform.position;

                // Find closest point in bottomSlice.OuterDestinations
                Vector3 p0 = Vector3.zero;
                if (bottomSlice.OuterDestinations != null && bottomSlice.OuterDestinations.Count > 0)
                {
                    float minDist = float.MaxValue;
                    foreach (var bp in bottomSlice.OuterDestinations)
                    {
                        float d = Vector3.SqrMagnitude(bp - referencePos);
                        if (d < minDist)
                        {
                            minDist = d;
                            p0 = bp;
                        }
                    }
                }
                else if (bottomSlice.Grabbers != null && g < bottomSlice.Grabbers.Count)
                {
                    p0 = bottomSlice.Grabbers[g].transform.position;
                }

                // Find closest point in topSlice.OuterDestinations
                Vector3 p2 = Vector3.zero;
                if (topSlice.OuterDestinations != null && topSlice.OuterDestinations.Count > 0)
                {
                    float minDist = float.MaxValue;
                    foreach (var tp in topSlice.OuterDestinations)
                    {
                        float d = Vector3.SqrMagnitude(tp - referencePos);
                        if (d < minDist)
                        {
                            minDist = d;
                            p2 = tp;
                        }
                    }
                }
                else if (topSlice.Grabbers != null && g < topSlice.Grabbers.Count)
                {
                    p2 = topSlice.Grabbers[g].transform.position;
                }

                // Calculate P1 (The Control Point)
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);
                Vector3 outwardDirection = (midpoint - currentAxisCenter).normalized;
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                // Calculate the final anatomical position
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                // Assign the calculated point to the missing slice's destination
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