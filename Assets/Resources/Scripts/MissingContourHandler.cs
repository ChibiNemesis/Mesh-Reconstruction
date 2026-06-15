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

    [SerializeField]
    [Tooltip("How smooth the drawn curve is in the Scene view (Number of line segments).")]
    private int GizmoResolution = 15;

    // Cache to hold the math data so OnDrawGizmos can draw it safely
    private struct DebugCurveData
    {
        public Vector3 p0, p1, p2, midpoint, centerLocked;
    }
    private List<DebugCurveData> _debugCurves = new List<DebugCurveData>();

    public void HandleMissingContours(List<SliceData> SliceGrabbers, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour,
        Vector3 FirstCentroid, Vector3 LastCentroid, int MissingContourCount, AxisCut axis)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;
        if (FirstContour == null || LastContour == null || FirstContour.sharedMesh == null || LastContour.sharedMesh == null) return;

        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);

        for (int i = FirstIndex + 1; i < LastIndex; i++)
        {
            SliceData missingSlice = SliceGrabbers[i];
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
                Vector3 undeformedPos = missingSlice.OuterDestinations[g];

                Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
                Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);

                // 1. Calculate the true mathematical midpoint (Stationary for the entire curve)
                Vector3 trueMidpoint = Vector3.Lerp(p0, p2, 0.5f);

                // 2. Calculate the purely 2D outward direction (Fixing the bug from your comments!)
                Vector3 flatUndeformed = undeformedPos;
                Vector3 flatCenter = gapCenter;

                if (axis == AxisCut.X) { flatUndeformed.x = 0; flatCenter.x = 0; }
                else if (axis == AxisCut.Y) { flatUndeformed.y = 0; flatCenter.y = 0; }
                else { flatUndeformed.z = 0; flatCenter.z = 0; }

                Vector3 outwardDirection = (flatUndeformed - flatCenter).normalized;

                // 3. Create a completely stable control point
                Vector3 p1 = trueMidpoint + (outwardDirection * BezierPower);

                // Debug Gizmos
                if (EnableGizmos && (DebugSpecificVertex == -1 || g == DebugSpecificVertex))
                {
                    if (i == FirstIndex + 1 && g == 0) _debugCurves.Clear();
                    _debugCurves.Add(new DebugCurveData
                    {
                        p0 = p0,
                        p1 = p1,
                        p2 = p2,
                        midpoint = trueMidpoint,
                        centerLocked = gapCenter
                    });
                }

                // 4. Compute the 3D Bezier coordinate
                Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

                // 5. THE PLANAR LOCK: Force the final target back onto the physical slice plane!
                targetPosition = new Vector3(
                    axis == AxisCut.X ? undeformedPos.x : targetPosition.x,
                    axis == AxisCut.Y ? undeformedPos.y : targetPosition.y,
                    axis == AxisCut.Z ? undeformedPos.z : targetPosition.z
                );

                missingSlice.OuterDestinations[g] = targetPosition;
            }
        }
    }

    public void HandleMissingContoursAdjacent(List<SliceData> SliceGrabbers, List<int> Misses, int sliceIndex, int FirstIndex, int LastIndex, MeshCollider FirstContour, MeshCollider LastContour, Vector3 FirstCentroid, Vector3 LastCentroid, AxisCut axis)
    {
        if (FirstIndex < 0 || LastIndex >= SliceGrabbers.Count || FirstIndex >= LastIndex) return;
        if (FirstContour == null || LastContour == null || FirstContour.sharedMesh == null || LastContour.sharedMesh == null) return;

        SliceData targetSlice = SliceGrabbers[sliceIndex];
        Vector3 gapCenter = Vector3.Lerp(FirstCentroid, LastCentroid, 0.5f);
        float t = (float)(sliceIndex - FirstIndex) / (LastIndex - FirstIndex);

        foreach (int g in Misses)
        {
            if (g < 0 || g >= targetSlice.Grabbers.Count) continue;

            Vector3 undeformedPos = targetSlice.OuterDestinations[g];

            Vector3 p0 = GetClosestVertexOnMesh(undeformedPos, FirstContour);
            Vector3 p2 = GetClosestVertexOnMesh(undeformedPos, LastContour);

            Vector3 trueMidpoint = Vector3.Lerp(p0, p2, 0.5f);

            Vector3 flatUndeformed = undeformedPos;
            Vector3 flatCenter = gapCenter;

            if (axis == AxisCut.X) { flatUndeformed.x = 0; flatCenter.x = 0; }
            else if (axis == AxisCut.Y) { flatUndeformed.y = 0; flatCenter.y = 0; }
            else { flatUndeformed.z = 0; flatCenter.z = 0; }

            Vector3 outwardDirection = (flatUndeformed - flatCenter).normalized;
            Vector3 p1 = trueMidpoint + (outwardDirection * BezierPower);

            Vector3 targetPosition = CalculateQuadraticBezierPoint(t, p0, p1, p2);

            // Enforce the planar slice lock for adjacent misses as well
            targetPosition = new Vector3(
                axis == AxisCut.X ? undeformedPos.x : targetPosition.x,
                axis == AxisCut.Y ? undeformedPos.y : targetPosition.y,
                axis == AxisCut.Z ? undeformedPos.z : targetPosition.z
            );

            targetSlice.OuterDestinations[g] = targetPosition;
        }
    }

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

    private void OnDrawGizmosSelected()
    {
        if (!EnableGizmos || _debugCurves == null || _debugCurves.Count == 0) return;

        foreach (var curve in _debugCurves)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(curve.p0, 0.002f);
            Gizmos.DrawWireSphere(curve.p2, 0.002f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(curve.midpoint, 0.002f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(curve.midpoint, curve.p1);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(curve.p1, 0.002f);

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