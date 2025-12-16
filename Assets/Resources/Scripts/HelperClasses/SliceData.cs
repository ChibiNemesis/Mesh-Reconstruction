using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using DelaunatorSharp;

//Store an AABB's Slice coordinates and particle grabbers
[System.Serializable, Inspectable]
public class SliceData
{
    public bool IsEdgeSlice;
    public Vector3 Min;
    public Vector3 Max;

    public List<GameObject> Grabbers;

    public List<GameObject> OuterGrabbers; // used on all slices
    public List<GameObject> InnerGrabbers; // used on first and last slice only
    public List<Vector3> OriginalOuterPositions;
    public List<Vector3> OriginalInnerPositions;

    //List of final dentinations for each particle
    public List<Vector3> Destinations;

    //Used for first and last slices
    public List<Vector3> OuterDestinations;
    public List<Vector3> InnerDestinations;

    public AxisCut axis = AxisCut.Y;

    //Script responsible for triangulating the Outer Grabbers
    private Delaunator delaunator = null;

    public SliceData()
    {
        Grabbers = new List<GameObject>();
        Min = Vector3.zero;
        Max = Vector3.zero;
    }

    public SliceData(Vector3 _min, Vector3 _max)
    {
        Grabbers = new List<GameObject>();
        Min = _min;
        Max = _max;
    }

    public void TriangulateOld()
    {
        IPoint[] points = new IPoint[Grabbers.Count];
        Vector2[] Inpoints = new Vector2[InnerGrabbers.Count];

        for (var g = 0; g < Grabbers.Count; g++)
        {
            Debug.Assert(Grabbers[g] != null);
            points[g] = new Point();

            var pos = Grabbers[g].transform.position;

            switch (axis)
            {
                case AxisCut.X:
                    points[g].X = pos.y;
                    points[g].Y = pos.z;
                    break;
                case AxisCut.Y:
                    points[g].X = pos.x;
                    points[g].Y = pos.z;
                    break;
                case AxisCut.Z:
                    points[g].X = pos.x;
                    points[g].Y = pos.y;
                    break;
            }
        }

        //Triangulate Outer Grabbers
        delaunator = new Delaunator(points);

        for (var g = 0; g < InnerGrabbers.Count; g++)
        {
            var pos = InnerGrabbers[g].transform.position;

            switch (axis)
            {
                case AxisCut.X:
                    Inpoints[g] = new Vector2(pos.y, pos.z);
                    break;
                case AxisCut.Y:
                    Inpoints[g] = new Vector2(pos.x, pos.z);
                    break;
                default: // Z
                    Inpoints[g] = new Vector2(pos.x, pos.y);
                    break;
            }
        }

        for (int i = 0; i < InnerGrabbers.Count; i++)
        {
            var Pos = Inpoints[i];
            bool matchFound = false;

            // Best candidate tracking (fallback)
            float minDistance = float.MaxValue;
            int bestT = -1;
            float bestU = 0, bestV = 0, bestW = 0;

            for (int t = 0; t < delaunator.Triangles.Length / 3; t++)
            {
                var i1 = delaunator.Triangles[3 * t];
                var i2 = delaunator.Triangles[3 * t + 1];
                var i3 = delaunator.Triangles[3 * t + 2];

                Vector2 p1 = new Vector2((float)points[i1].X, (float)points[i1].Y);
                Vector2 p2 = new Vector2((float)points[i2].X, (float)points[i2].Y);
                Vector2 p3 = new Vector2((float)points[i3].X, (float)points[i3].Y);

                // Compute Barycentric first to check inclusion
                float u, v, w;
                ComputeBarycentric(Pos, p1, p2, p3, out u, out v, out w);

                // Check if inside (Relaxed epsilon for edge cases)
                // Using a small negative epsilon allows points slightly on the line or outside to pass
                if (u >= -0.001f && v >= -0.001f && w >= -0.001f)
                {
                    SetGrabberData(InnerGrabbers[i], i1, i2, i3, u, v, w);
                    matchFound = true;
                    break;
                }

                // Calculate distance to triangle centroid for fallback
                Vector2 center = (p1 + p2 + p3) / 3f;
                float dist = Vector2.Distance(Pos, center);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestT = t;
                    bestU = u; bestV = v; bestW = w;
                }
            }

            // FALLBACK: If point was not strictly inside any triangle
            if (!matchFound && bestT != -1)
            {
                var i1 = delaunator.Triangles[3 * bestT];
                var i2 = delaunator.Triangles[3 * bestT + 1];
                var i3 = delaunator.Triangles[3 * bestT + 2];

                // Note: Barycentric coords might be outside [0,1], but that's mathematically valid 
                // for points outside the triangle (Extrapolation)
                SetGrabberData(InnerGrabbers[i], i1, i2, i3, bestU, bestV, bestW);
            }
        }
}

    public void Triangulate()
    {
        // --- STEP 1: DOWN-SAMPLING (The Fix) ---
        // We select a subset of indices from Grabbers to act as "Anchors" for the triangulation.
        // This creates "fat", stable triangles while ignoring the dense, noisy vertices in between.

        List<int> anchorIndices = new List<int>();
        List<IPoint> anchorPoints = new List<IPoint>();

        // Adjust this threshold based on model scale! 
        // 0.05f (5cm) is usually a good starting point for human anatomy.
        // Larger = more stable but less detailed boundary. Smaller = more detail but risk of slivers.
        float minSamplingDist = 0.05f;

        if (Grabbers.Count > 0)
        {
            // Always add the first point
            anchorIndices.Add(0);
            anchorPoints.Add(PointFromGrabber(Grabbers[0], axis));

            Vector2 lastP = ProjectTo2D(Grabbers[0].transform.position, axis);

            for (int i = 1; i < Grabbers.Count; i++)
            {
                Vector2 currP = ProjectTo2D(Grabbers[i].transform.position, axis);

                // Only add if far enough from the last added anchor
                if (Vector2.Distance(currP, lastP) > minSamplingDist)
                {
                    anchorIndices.Add(i);
                    anchorPoints.Add(PointFromGrabber(Grabbers[i], axis));
                    lastP = currP;
                }
            }

            // Safety: Ensure we didn't simplify it down to a line or point (need at least 3)
            if (anchorIndices.Count < 3)
            {
                // Fallback: If simplified too much, use every Nth point to guarantee a shape
                // Or just force the original list if it's very small
                anchorIndices.Clear();
                anchorPoints.Clear();
                for (int i = 0; i < Grabbers.Count; i++)
                {
                    anchorIndices.Add(i);
                    anchorPoints.Add(PointFromGrabber(Grabbers[i], axis));
                }
            }
        }

        // --- STEP 2: TRIANGULATE THE ANCHORS ---
        // Delaunator now runs on the small, stable list
        delaunator = new Delaunator(anchorPoints.ToArray());

        // Prepare Inner Points for mapping
        Vector2[] Inpoints = new Vector2[InnerGrabbers.Count];
        for (var g = 0; g < InnerGrabbers.Count; g++)
        {
            Inpoints[g] = ProjectTo2D(InnerGrabbers[g].transform.position, axis);
        }

        // --- STEP 3: MAPPING INNER GRABBERS ---
        for (int i = 0; i < InnerGrabbers.Count; i++)
        {
            var Pos = Inpoints[i];
            bool matchFound = false;

            float minDistance = float.MaxValue;
            int bestT = -1;
            float bestU = 0, bestV = 0, bestW = 0;

            // Iterate through the simplified triangles
            for (int t = 0; t < delaunator.Triangles.Length / 3; t++)
            {
                // Get indices relative to the ANCHOR list (0, 1, 2...)
                int localA = delaunator.Triangles[3 * t];
                int localB = delaunator.Triangles[3 * t + 1];
                int localC = delaunator.Triangles[3 * t + 2];

                // Convert to ACTUAL COORDINATES using the anchorPoints list
                Vector2 p1 = new Vector2((float)anchorPoints[localA].X, (float)anchorPoints[localA].Y);
                Vector2 p2 = new Vector2((float)anchorPoints[localB].X, (float)anchorPoints[localB].Y);
                Vector2 p3 = new Vector2((float)anchorPoints[localC].X, (float)anchorPoints[localC].Y);

                float u, v, w;
                ComputeBarycentric(Pos, p1, p2, p3, out u, out v, out w);

                // Check if inside (with epsilon)
                if (u >= -0.01f && v >= -0.01f && w >= -0.01f)
                {
                    // CRITICAL STEP: Retrieve the ORIGINAL indices from the main Grabbers list
                    int originalIndexA = anchorIndices[localA];
                    int originalIndexB = anchorIndices[localB];
                    int originalIndexC = anchorIndices[localC];

                    SetGrabberData(InnerGrabbers[i], originalIndexA, originalIndexB, originalIndexC, u, v, w);
                    matchFound = true;
                    break;
                }

                // Fallback tracking
                Vector2 center = (p1 + p2 + p3) / 3f;
                float dist = Vector2.Distance(Pos, center);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestT = t;
                    bestU = u; bestV = v; bestW = w;
                }
            }

            // Apply Fallback
            if (!matchFound && bestT != -1)
            {
                int localA = delaunator.Triangles[3 * bestT];
                int localB = delaunator.Triangles[3 * bestT + 1];
                int localC = delaunator.Triangles[3 * bestT + 2];

                // Use ANCHOR list to find original indices
                int originalIndexA = anchorIndices[localA];
                int originalIndexB = anchorIndices[localB];
                int originalIndexC = anchorIndices[localC];

                SetGrabberData(InnerGrabbers[i], originalIndexA, originalIndexB, originalIndexC, bestU, bestV, bestW);
            }
        }
    }

    // Helper to project Grabber directly to Delaunator Point
    private IPoint PointFromGrabber(GameObject g, AxisCut axis)
    {
        var pos = g.transform.position;
        switch (axis)
        {
            case AxisCut.X: return new Point(pos.y, pos.z);
            case AxisCut.Y: return new Point(pos.x, pos.z);
            default: return new Point(pos.x, pos.y);
        }
    }

    private Vector2 ProjectTo2D(Vector3 p, AxisCut axis)
    {
        switch (axis)
        {
            case AxisCut.X: return new Vector2(p.y, p.z);
            case AxisCut.Y: return new Vector2(p.x, p.z);
            default: return new Vector2(p.x, p.y);
        }
    }

    // Helper to clean up the loop code
    private void SetGrabberData(GameObject obj, int i1, int i2, int i3, float u, float v, float w)
    {
        var pg = obj.GetComponent<ParticleGrab>();
        pg.SetTriangleIndices(i1, i2, i3);
        pg.SetBarycentric(new Vector3(u, v, w)); // Note: Your ComputeBarycentric outputs u/v/w differently than Vector3 components, ensure order matches your consumption logic.
    }

    //Check whether a points is inside a triangle created by 3 other points
    private bool PointInTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
    {
        float w1 = (a.x * (c.y - a.y) + (p.y - a.y) * (c.x - a.x) - p.x * (c.y - a.y)) /
           ((b.y - a.y) * (c.x - a.x) - (b.x - a.x) * (c.y - a.y));

        float w2 = (p.y - a.y - w1 * (b.y - a.y)) / (c.y - a.y);

        return w1 >= 0f && w2 >= 0f && (w1 + w2) <= 1f;
    }

    //a, b, c form triangle, p must be inside the triangle (Cramer's rule)
    private void ComputeBarycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out float u, out float v, out float w)
    {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = p - a;

        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0);
        float d21 = Vector2.Dot(v2, v1);

        float denom = d00 * d11 - d01 * d01;

        v = (d11 * d20 - d01 * d21) / denom;
        w = (d00 * d21 - d01 * d20) / denom;
        u = 1.0f - v - w;
    }
}