using System.Collections.Generic;
using UnityEngine;

public class MissingContourHandler : MonoBehaviour
{
    [SerializeField]
    [Tooltip("How much the interpolated curve bulges outward. 0 = straight line.")]
    private float BezierPower = 0.4f;

    /// <summary>
    /// Handles the missing contours between two found contours.
    /// </summary>
    public void HandleMissingContours(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount)
    {
        // Safety checks
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];

        // 1. Calculate the central axis to determine "Outward"
        Vector3 bottomCentroid = FirstCentroid; // Replace with your centroid variable
        Vector3 topCentroid = LastCentroid;       // Replace with your centroid variable

        // 2. Loop through every missing slice in the gap
        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];

            // Calculate 't' (0.0 to 1.0). 
            // e.g., If there are 3 missing slices, t will be 0.25, 0.50, and 0.75
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            // Interpolate the centroid for this specific missing height
            Vector3 currentAxisCenter = Vector3.Lerp(bottomCentroid, topCentroid, t);

            // 3. Process each Grabber in the missing slice
            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                var grabber = missingSlice.Grabbers[g];

                // Anchor Points (P0 and P2)
                // Note: If your slices don't have matching indices, replace this with an angular matching function!
                Vector3 p0 = bottomSlice.Grabbers[g].transform.position;
                Vector3 p2 = topSlice.Grabbers[g].transform.position;

                // Calculate the Control Point (P1)
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // Determine the outward direction away from the center of the organ
                Vector3 outwardDirection = (midpoint - currentAxisCenter).normalized;

                // Push the midpoint outward to create the bezier "belly"
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                // Calculate final anatomical position
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                // Apply to the missing Grabber
                // (If your Grabbers move via physics, you might set their target position instead of transform)
                grabber.transform.position = targetPosition;
            }
        }
    }

    /// <summary>
    /// Computes the point on a quadratic bezier curve at parameter t.
    /// </summary>
    private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        // B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;

        Vector3 p = uu * p0;       // (1-t)^2 * P0
        p += 2 * u * t * p1;       // 2(1-t)t * P1
        p += tt * p2;              // t^2 * P2

        return p;
    }
}
