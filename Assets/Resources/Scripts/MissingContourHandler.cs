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

    /// <summary>
    /// Handles the missing contours between two found contours.
    /// </summary>
    public void HandleMissingContours2(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount)
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
    /// Handles the missing contours between two found contours.
    /// </summary>
    public void HandleMissingContours3(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;

        cachedSlices = SliceGrabbers;
        cachedFirstIndex = FirstIndex;
        cachedLastIndex = LastIndex;
        cachedFirstCentroid = FirstCentroid;
        cachedLastCentroid = LastCentroid;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];

        // Center of the entire missing gap (used for outward Bezier bulging)
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // Safely get the corresponding vertex from the bottom slice
                Vector3 p0 = bottomSlice.OuterDestinations != null && bottomSlice.OuterDestinations.Count > g
                    ? bottomSlice.OuterDestinations[g]
                    : bottomSlice.Grabbers[g].transform.position;

                // Safely get the corresponding vertex from the top slice
                // Added a safety check in case the top slice has fewer grabbers
                int topIndex = Mathf.Min(g, topSlice.Grabbers.Count - 1);
                Vector3 p2 = topSlice.OuterDestinations != null && topSlice.OuterDestinations.Count > topIndex
                    ? topSlice.OuterDestinations[topIndex]
                    : topSlice.Grabbers[topIndex].transform.position;

                // Calculate the midpoint exactly as the Gizmo does
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // Push outward from the center of the gap exactly as the Gizmo does
                Vector3 outwardDirection = (midpoint - gapCenter).normalized;
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                // Compute the final bezier position
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    /// <summary>
    /// Handles the missing contours between two found contours.
    /// </summary>
    public void HandleMissingContours4(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;

        cachedSlices = SliceGrabbers;
        cachedFirstIndex = FirstIndex;
        cachedLastIndex = LastIndex;
        cachedFirstCentroid = FirstCentroid;
        cachedLastCentroid = LastCentroid;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];

        // The center of the gap, used purely to know which way is "outward" for the Bezier bulge
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            Vector3 currentAxisCenter = Vector3.Lerp(FirstCentroid, LastCentroid, t);

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                Vector3 undeformedPos = missingSlice.Grabbers[g].transform.position;

                // We use this local ray strictly for finding the matching vertices (preventing twists)
                Vector3 localRadialDir = (undeformedPos - currentAxisCenter).normalized;

                // ---------------------------------------------------------
                // 1. FIND MATCHING P0 (Bottom Slice) using Dot Product
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

                // CRITICAL FIX: Just take the literal position. No radius calculations.
                Vector3 p0 = bottomSlice.OuterDestinations != null && bottomSlice.OuterDestinations.Count > bestBottomIndex
                    ? bottomSlice.OuterDestinations[bestBottomIndex]
                    : bottomSlice.Grabbers[bestBottomIndex].transform.position;


                // ---------------------------------------------------------
                // 2. FIND MATCHING P2 (Top Slice) using Dot Product
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

                // CRITICAL FIX: Just take the literal position. No radius calculations.
                Vector3 p2 = topSlice.OuterDestinations != null && topSlice.OuterDestinations.Count > bestTopIndex
                    ? topSlice.OuterDestinations[bestTopIndex]
                    : topSlice.Grabbers[bestTopIndex].transform.position;


                // ---------------------------------------------------------
                // 3. CALCULATE BEZIER CURVE
                // ---------------------------------------------------------
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // Push outward from the center of the bone/organ using your 0.005 / 0.001 power
                Vector3 outwardDirection = (midpoint - gapCenter).normalized;
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    /// <summary>
    /// Handles the missing contours between two found contours using Undeformed Nearest-Neighbor matching
    /// to guarantee zero topological twisting or fat bulging.
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

        // Center of the gap (used purely to know which way is "out" for the Bezier bulge)
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // The pure, undeformed position of this specific missing vertex
                //Vector3 undeformedPos = missingSlice.Grabbers[g].transform.position;
                Vector3 undeformedPos = missingSlice.OuterDestinations[g];

                // ---------------------------------------------------------
                // 1. FIND MATCHING P0 (Bottom Slice) via Undeformed Distance
                // ---------------------------------------------------------
                int bestBottomIndex = 0;
                float minBottomDist = float.MaxValue;

                for (int b = 0; b < bottomSlice.Grabbers.Count; b++)
                {
                    // We check distance in UNDEFORMED space to find the exact vertex sitting directly below it
                    float dist = Vector3.Distance(undeformedPos, bottomSlice.Grabbers[b].transform.position);
                    if (dist < minBottomDist)
                    {
                        minBottomDist = dist;
                        bestBottomIndex = b;
                    }
                }

                // Get the literal Deformed position of that matched bottom vertex
                Vector3 p0 = bottomSlice.OuterDestinations != null && bottomSlice.OuterDestinations.Count > bestBottomIndex
                    ? bottomSlice.OuterDestinations[bestBottomIndex]
                    : bottomSlice.Grabbers[bestBottomIndex].transform.position;


                // ---------------------------------------------------------
                // 2. FIND MATCHING P2 (Top Slice) via Undeformed Distance
                // ---------------------------------------------------------
                int bestTopIndex = 0;
                float minTopDist = float.MaxValue;

                for (int top = 0; top < topSlice.Grabbers.Count; top++)
                {
                    // We check distance in UNDEFORMED space to find the exact vertex sitting directly above it
                    float dist = Vector3.Distance(undeformedPos, topSlice.Grabbers[top].transform.position);
                    if (dist < minTopDist)
                    {
                        minTopDist = dist;
                        bestTopIndex = top;
                    }
                }

                // Get the literal Deformed position of that matched top vertex
                Vector3 p2 = topSlice.OuterDestinations != null && topSlice.OuterDestinations.Count > bestTopIndex
                    ? topSlice.OuterDestinations[bestTopIndex]
                    : topSlice.Grabbers[bestTopIndex].transform.position;


                // ---------------------------------------------------------
                // 3. CALCULATE BEZIER CURVE
                // ---------------------------------------------------------
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // Push outward from the center using your micro-adjustments (0.005 / 0.001)
                Vector3 outwardDirection = (midpoint - gapCenter).normalized;
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }


    /// <summary>
    /// Handles missing contours by finding the absolute closest physical points on the adjacent valid contours,
    /// creating a patient-data-anchored Bezier curve to interpolate the missing gap.
    /// </summary>
    public void HandleMissingContoursv2(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount, AxisCut axis)
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

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // Great catch switching to OuterDestinations to ensure pre-scaled proportions are maintained!
                Vector3 undeformedPos = missingSlice.OuterDestinations[g];
                //Debug.Log("Undeformed Position: " + undeformedPos);

                // 1. Find P0 and P2 on the patient data
                Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
                Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);

                // 3. Calculate the midpoint
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // 4. Calculate P1 (The Control Point)
                // CRITICAL FIX: Lock the gap center to the exact axis coordinate of the MIDPOINT.
                // This guarantees the (midpoint - center) vector has an axis value of exactly 0, 
                // resulting in a perfectly flat, 2D outward push.
                Vector3 gapCenterLocked;
                switch (axis)
                {
                    case AxisCut.X:
                        gapCenterLocked = new Vector3(undeformedPos.x, gapCenter.y, gapCenter.z);
                        break;
                    case AxisCut.Y:
                        gapCenterLocked = new Vector3(gapCenter.x, undeformedPos.y, gapCenter.z);
                        break;
                    case AxisCut.Z:
                    default:
                        gapCenterLocked = new Vector3(gapCenter.x, gapCenter.y, undeformedPos.z);
                        break;
                }

                Vector3 outwardDirection = (midpoint - gapCenterLocked).normalized;
                Vector3 p1 = midpoint + (outwardDirection * BezierPower);

                // 5. Compute the final Bezier coordinate and assign it
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);
                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    public void HandleMissingContoursv3(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour, Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount, AxisCut axis)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;
        if (FirstContour == null || LastContour == null || FirstContour.sharedMesh == null || LastContour.sharedMesh == null) return;

        SliceData bottomSlice = SliceGrabbers[FirstIndex];
        SliceData topSlice = SliceGrabbers[LastIndex];
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
            float t = (float)(i - FirstIndex) / (LastIndex - FirstIndex);

            if (missingSlice.OuterDestinations == null || missingSlice.OuterDestinations.Count != missingSlice.Grabbers.Count)
            {
                missingSlice.OuterDestinations = new List<Vector3>(new Vector3[missingSlice.Grabbers.Count]);
            }

            for (int g = 0; g < missingSlice.Grabbers.Count; g++)
            {
                // BUG FIX: Must read from the Transform to get real 3D coordinates, NOT the empty array!
                Vector3 undeformedPos = missingSlice.Grabbers[g].transform.position;

                // 1. Find P0 and P2 on the exact patient contours
                Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
                Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);
                Vector3 midpoint = Vector3.Lerp(p0, p2, 0.5f);

                // 2. THE DYNAMIC CURVATURE UPGRADE
                // Read the original geometry to see how far this specific vertex naturally bows outward.
                int bIndex = Mathf.Min(g, bottomSlice.Grabbers.Count - 1);
                int tIndex = Mathf.Min(g, topSlice.Grabbers.Count - 1);

                Vector3 origP0 = bottomSlice.Grabbers[bIndex].transform.position;
                Vector3 origP2 = topSlice.Grabbers[tIndex].transform.position;
                Vector3 origMidpoint = Vector3.Lerp(origP0, origP2, 0.5f);

                // The natural curve designed by the 3D artist
                float naturalBulge = Vector3.Distance(origMidpoint, undeformedPos);

                // 3. CALCULATE P1 (Control Point)
                Vector3 gapCenterLocked;
                switch (axis)
                {
                    case AxisCut.X: gapCenterLocked = new Vector3(midpoint.x, gapCenter.y, gapCenter.z); break;
                    case AxisCut.Y: gapCenterLocked = new Vector3(gapCenter.x, midpoint.y, gapCenter.z); break;
                    case AxisCut.Z: default: gapCenterLocked = new Vector3(gapCenter.x, gapCenter.y, midpoint.z); break;
                }

                Vector3 outwardDirection = (midpoint - gapCenterLocked).normalized;

                // MATHEMATICAL TRICK: For a Quadratic Bezier curve to reach a desired distance at t=0.5, 
                // the control point (P1) must be pushed out EXACTLY TWICE that distance.
                float dynamicBezierPower = naturalBulge * 2f;
                Vector3 p1 = midpoint + (outwardDirection * dynamicBezierPower);

                // 4. Compute and assign the final coordinate
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
                Vector3 outwardDirection = (undeformedPos - gapCenterLocked).normalized;

                // Apply your manual Bezier power
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
    /*private void OnDrawGizmosSelected()
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
    }*/
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