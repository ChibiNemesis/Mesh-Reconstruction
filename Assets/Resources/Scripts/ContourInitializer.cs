using DelaunatorSharp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


//A class that for initializing slices based on contours
public class ContourInitializer : SliceInitializer
{
    [SerializeField]
    SliceReshaper shaper;

    //GameObject containing planar contours
    [SerializeField]
    GameObject Contour;

    public List<MeshFilter> ContourSlices;

    [SerializeField]
    [Range(0f, 0.1f)]
    float SamplingFactor = 0.05f;

    //private Dictionary<ParticleGrab, HashSet<ParticleGrab>> GrabberAdjacency;

    private void Start()
    {
        ContourSlices = new List<MeshFilter>();

        for (int c = 0; c < Contour.transform.childCount; c++)
        {
            var mesh = Contour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>();
            ContourSlices.Add(mesh);
        }
        //GrabberAdjacency = GetComponent<SliceReshaper>().GrabberAdjacency;
    }

    public SamplingMode GetSamplingMethod()
    {
        return SamplingMethod;
    }

    public override void InitializeSlices()
    {
        InitializationV6();
    }

    private void InitializationV6()
    {
        if (shaper == null || Contour == null)
        {
            Debug.LogWarning("Missing reference to SliceReshaper or Contour.");
            return;
        }

        AxisCut axis = shaper.GetComponent<BoundsSlicer>().GetAxis();
        int contourCount = ContourSlices.Count;
        int sliceCount = shaper.SliceGrabbers.Count;

        if (contourCount == 0 || sliceCount == 0)
        {
            Debug.LogWarning("No contour or slice data available.");
            return;
        }

        // Compute positions along slicing axis
        List<float> contourAxisPositions = new List<float>();
        for (int c = 0; c < ContourSlices.Count; c++)
        {
            var mf = ContourSlices[c];
            float coord = 0f;

            if (mf == null)
            {
                var posalt = Contour.transform.GetChild(c).position;
                coord = axis switch
                {
                    AxisCut.X => posalt.x,
                    AxisCut.Y => posalt.y,
                    AxisCut.Z => posalt.z,
                    _ => 0f
                };
            }
            else
            {
                Bounds b = mf.sharedMesh.bounds;
                Vector3 pos = mf.transform.position;
                coord = axis switch
                {
                    AxisCut.X => pos.x + b.center.x,
                    AxisCut.Y => pos.y + b.center.y,
                    AxisCut.Z => pos.z + b.center.z,
                    _ => 0f
                };
            }

            contourAxisPositions.Add(coord);
        }

        // Sort contours by axis
        SortContoursByAxis(ref ContourSlices, ref contourAxisPositions);

        List<int> EmptyContours = new List<int>(); // Stores indices of missing contours. When a new existing contour is found, the list gets cleared
        int lastFull = -1; // Stores last existing contour's index (in case interpolation is needed)

        for (int s = 0; s < sliceCount; s++)
        {
            SliceData slice = shaper.SliceGrabbers[s];
            slice.Destinations = new List<Vector3>();
            List<GameObject> grabbers = slice.Grabbers;
            if (grabbers.Count == 0)
                continue;

            //If slice is empty, add slice to empty contours and continue
            if (ContourSlices[s] == null)
            {
                EmptyContours.Add(s);
                continue;
            }

            bool IsEdgeSlice = (s == 0 || s == sliceCount - 1);

            slice.IsEdgeSlice = IsEdgeSlice;
            if (IsEdgeSlice)
            {
                slice.InnerGrabbers = new List<GameObject>();
                slice.OuterGrabbers = new List<GameObject>();
                slice.InnerDestinations = new List<Vector3>();
                slice.OuterDestinations = new List<Vector3>();
                slice.OriginalInnerPositions = new List<Vector3>();
                slice.OriginalOuterPositions = new List<Vector3>();
                SplitOuterInner(slice, axis);

                SortGrabbersCounterClockwiseInPlace(slice.Grabbers, axis);

                List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
                List<Vector3> Samples = SamplePoints(Boundary, slice.Grabbers.Count);

                // compute plane coordinate based on grabbers (so both sets use same plane)
                var FullPos = GetGrabberPositions(slice.Grabbers);
                float planeCoord = ComputeAxisPlaneCoord(FullPos, axis);


                // project both to that plane (temporary lists) to get a stable 2D normal
                var gPosPlanar = ProjectToSlicePlane(FullPos, axis, planeCoord);// Projecy since Newel's method works on 2d only (all points on same plane)
                var samplesPlanar = ProjectToSlicePlane(Samples, axis, planeCoord);

                // compute normals from planar sets (Newell on planar points -> robust)
                Vector3 grabberNormal = (gPosPlanar.Count >= 3) ? ComputePolygonNormal(gPosPlanar) : GetPlaneNormal(axis);
                Vector3 sampleNormal = (samplesPlanar.Count >= 3) ? ComputePolygonNormal(samplesPlanar) : GetPlaneNormal(axis);

                // flip samples if their winding disagrees with grabbers
                if (Vector3.Dot(grabberNormal, sampleNormal) < 0f)
                {
                    Samples.Reverse();
                }

                Samples = SortPointsCounterClockwise(Samples, axis);

                // Align sample start index to grabbers' start index 
                AlignSamplesToGrabbers(Samples, slice.Grabbers, axis);

                //Save all initial positions after sorting
                foreach(var g in slice.InnerGrabbers)
                {
                    slice.OriginalInnerPositions.Add(g.transform.position);
                }
                foreach (var g in slice.OuterGrabbers)
                {
                    slice.OriginalOuterPositions.Add(g.transform.position);
                }

                // Sync OuterGrabbers list to match slice.Grabbers order exactly
                slice.OuterGrabbers = new List<GameObject>(slice.Grabbers);

                // Iterate using slice.Grabbers (which is now sorted CCW)
                for (int i = 0; i < slice.Grabbers.Count; i++)
                {
                    Vector3 grabberPos = slice.Grabbers[i].transform.position;
                    Vector3 target = Samples[i];

                    switch (axis)
                    {
                        case AxisCut.X: target.x = grabberPos.x; break;
                        case AxisCut.Y: target.y = grabberPos.y; break;
                        case AxisCut.Z: target.z = grabberPos.z; break;
                    }

                    slice.Destinations.Add(target);
                    // Optional: Add to OuterDestinations if you use it separately
                    slice.OuterDestinations.Add(target);
                }

                /*for (int i = 0; i < slice.OuterGrabbers.Count; i++)
                {
                    Vector3 grabberPos = slice.OuterGrabbers[i].transform.position;
                    Vector3 target = Samples[i];

                    switch (axis)
                    {
                        case AxisCut.X: target.x = grabberPos.x; break;
                        case AxisCut.Y: target.y = grabberPos.y; break;
                        case AxisCut.Z: target.z = grabberPos.z; break;
                    }

                    //slice.OuterDestinations.Add(target);
                    slice.Destinations.Add(target);
                }*/
            }
            else
            {
                // Sort grabbers CCW using global angular reference
                SortGrabbersCounterClockwiseInPlace(slice.Grabbers, axis);

                // Get contour boundary and sample points
                List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
                List<Vector3> Samples = SamplePoints(Boundary, slice.Grabbers.Count);

                // compute plane coordinate based on grabbers (so both sets use same plane)
                var gPos = GetGrabberPositions(grabbers);
                float planeCoord = ComputeAxisPlaneCoord(gPos, axis);

                // project both to that plane (temporary lists) to get a stable 2D normal
                var gPosPlanar = ProjectToSlicePlane(gPos, axis, planeCoord);
                var samplesPlanar = ProjectToSlicePlane(Samples, axis, planeCoord);

                // compute normals from planar sets (Newell on planar points -> robust)
                Vector3 grabberNormal = (gPosPlanar.Count >= 3) ? ComputePolygonNormal(gPosPlanar) : GetPlaneNormal(axis);
                Vector3 sampleNormal = (samplesPlanar.Count >= 3) ? ComputePolygonNormal(samplesPlanar) : GetPlaneNormal(axis);

                // flip samples if their winding disagrees with grabbers
                if (Vector3.Dot(grabberNormal, sampleNormal) < 0f)
                {
                    Samples.Reverse();
                }

                // sort samples CCW (global reference)
                Samples = SortPointsCounterClockwise(Samples, axis);

                // Align sample start index to grabbers' start index 
                AlignSamplesToGrabbers(Samples, grabbers, axis);

                // Assign destinations 1-to-1 
                for (int i = 0; i < grabbers.Count; i++)
                {
                    Vector3 grabberPos = grabbers[i].transform.position;
                    Vector3 target = Samples[i];

                    // Lock slicing axis
                    switch (axis)
                    {
                        case AxisCut.X: target.x = grabberPos.x; break;
                        case AxisCut.Y: target.y = grabberPos.y; break;
                        case AxisCut.Z: target.z = grabberPos.z; break;
                    }
                    slice.Destinations.Add(target);
                }
            }

            // Interpolation for missing slices
            if (EmptyContours.Count > 0)
            {
                if (lastFull == -1)
                {
                    // First contour of the slice is empty -> replicate first real slice when found
                    foreach (int missingIndex in EmptyContours)
                    {
                        var mSlice = shaper.SliceGrabbers[missingIndex];
                        mSlice.Destinations = new List<Vector3>(slice.Destinations);
                    }

                    EmptyContours.Clear();
                }
                else
                {
                    // We have lower and upper anchor slices
                    var lowerSlice = shaper.SliceGrabbers[lastFull];
                    var upperSlice = slice;

                    // Compute curvature strength using slice centers
                    Vector3 lowerCenter = (lowerSlice.Min + lowerSlice.Max) * 0.5f;
                    Vector3 upperCenter = (upperSlice.Min + upperSlice.Max) * 0.5f;

                    float bezierStrength = ComputeBezierStrength(lowerCenter, upperCenter);

                    var LastPos = shaper.SliceGrabbers[lastFull].Destinations;
                    var CurrPos = slice.Destinations;

                    for (int idx = 0; idx < EmptyContours.Count; idx++)
                    {
                        int sliceIndex = EmptyContours[idx];
                        var missingSlice = shaper.SliceGrabbers[sliceIndex];

                        // 1. Sort grabbers of the missing slice before applying destinations
                        SortGrabbersCounterClockwiseInPlace(missingSlice.Grabbers, axis);

                        // 2. Compute t along the gap
                        float t = (float)(idx + 1) / (EmptyContours.Count + 1);

                        var interpolated = BezierInterpolateSlices(LastPos, CurrPos, t, bezierStrength);

                        // Ensure interpolated winding matches the missing slice's grabbers
                        // First sort missing slice grabbers (you already do this earlier, but be safe)
                        SortGrabbersCounterClockwiseInPlace(missingSlice.Grabbers, axis);

                        // Align interpolated points to missing slice grabbers
                        // Convert interpolated to List<Vector3> (it already is) and rotate to match
                        AlignSamplesToGrabbers(interpolated, missingSlice.Grabbers, axis);

                        // Finally assign
                        missingSlice.Destinations = new List<Vector3>(interpolated);
                    }

                    EmptyContours.Clear();
                }
            }

            //Change inner points of top and bottom slice
            if (s == 0 || s == sliceCount - 1)
            {

                if(slice.InnerGrabbers!=null && slice.InnerGrabbers.Count > 0)
                {
                    // Match Destinations
                    slice.OuterDestinations = new List<Vector3>(slice.Destinations);

                    slice.Triangulate();
                    DeformInnerGrabbersForEdgeSlice(slice);
                }
                //temporary do not change inner grabbers
                /*for (int g = 0; g < slice.InnerGrabbers.Count; g++)
                {
                    slice.InnerDestinations.Add(slice.InnerGrabbers[g].transform.position);
                }*/
            }

            lastFull = s;
        }
        //Handle case if last slice is empty
        if (EmptyContours.Count > 0 && lastFull != -1)
        {
            var lastSliceDest = shaper.SliceGrabbers[lastFull].Destinations;

            foreach (int idx in EmptyContours)
            {
                shaper.SliceGrabbers[idx].Destinations = new List<Vector3>(lastSliceDest);
            }

            EmptyContours.Clear();
        }
        SmoothSliceDestinations(iterations: 6, lambda: 0.5f, preserveEndpoints: true);
    }

    //Helper Methods

    /// Sorts a list of grabbers in-place counter-clockwise on the given plane (axis) using a global reference.
    public static void SortGrabbersCounterClockwiseInPlace(List<GameObject> grabbers, AxisCut axis = AxisCut.Y)
    {
        if (grabbers == null || grabbers.Count < 3)
            return;

        // Compute centroid
        Vector3 center = GetCentroid(grabbers);

        // Compute angles for each grabber relative to centroid
        grabbers.Sort((a, b) =>
        {
            float angleA = GetAngleOnPlane(a.transform.position - center, axis);
            float angleB = GetAngleOnPlane(b.transform.position - center, axis);
            return angleA.CompareTo(angleB);
        });
    }

    private Vector3 GetPlaneNormal(AxisCut axis)
    {
        return axis switch
        {
            AxisCut.X => Vector3.right,
            AxisCut.Y => Vector3.up,
            AxisCut.Z => Vector3.forward,
            _ => Vector3.up
        };
    }

    private Vector3 ComputePolygonNormal(List<Vector3> points)
    {
        // Uses Newell’s method
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 current = points[i];
            Vector3 next = points[(i + 1) % points.Count];
            normal.x += (current.y - next.y) * (current.z + next.z);
            normal.y += (current.z - next.z) * (current.x + next.x);
            normal.z += (current.x - next.x) * (current.y + next.y);
        }
        return normal.normalized;
    }

    /// Sorts a list of Vector3 points counter-clockwise using a global reference on a given axis.
    public static List<Vector3> SortPointsCounterClockwise(List<Vector3> points, AxisCut axis = AxisCut.Y)
    {
        if (points == null || points.Count < 3)
            return new List<Vector3>(points);

        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        // Sort points by angle around centroid
        points.Sort((a, b) =>
        {
            float angleA = GetAngleOnPlane(a - center, axis);
            float angleB = GetAngleOnPlane(b - center, axis);
            return angleA.CompareTo(angleB);
        });

        return points;
    }

    /// Returns the centroid of a list of GameObjects.
    private static Vector3 GetCentroid(List<GameObject> objects)
    {
        Vector3 center = Vector3.zero;
        foreach (var obj in objects)
            center += obj.transform.position;
        center /= objects.Count;
        return center;
    }

    // Computes the angle of a vector projected on the given plane (axis) relative to +X direction.
    private static float GetAngleOnPlane(Vector3 vector, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.X => Mathf.Atan2(vector.z, vector.y),
            AxisCut.Y => Mathf.Atan2(vector.z, vector.x),
            AxisCut.Z => Mathf.Atan2(vector.y, vector.x),
            _ => Mathf.Atan2(vector.z, vector.x)
        };
    }

    private void SortContoursByAxis(ref List<MeshFilter> contours, ref List<float> positions)
    {
        var combined = new List<(MeshFilter, float)>();
        for (int i = 0; i < contours.Count; i++)
            combined.Add((contours[i], positions[i]));
        //combined.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        combined.Sort((a, b) => b.Item2.CompareTo(a.Item2));

        contours = new List<MeshFilter>();
        positions = new List<float>();
        foreach (var pair in combined)
        {
            contours.Add(pair.Item1);
            positions.Add(pair.Item2);
        }
    }

    private List<Vector3> GetOrderedBoundaryWorld(MeshFilter mf)
    {
        if (mf == null || mf.sharedMesh == null)
            return new List<Vector3>();

        var rawLoop = GetBoundaryLoop(mf.sharedMesh);
        if (rawLoop == null || rawLoop.Count < 3)
        {
            Debug.LogWarning($"Contour {mf.name} has invalid boundary (loop size {rawLoop?.Count ?? 0}).");
            return new List<Vector3>();
        }

        Mesh mesh = mf.sharedMesh;
        List<Vector3> boundary = GetBoundaryLoop(mesh);
        boundary = OrderBoundaryLoop(boundary);
        for (int i = 0; i < boundary.Count; i++)
            boundary[i] = mf.transform.TransformPoint(boundary[i]);
        return boundary;
    }

    private List<Vector3> SamplePoints(List<Vector3> Boundary, int count)
    {
        List<Vector3> Samples;
        if (SamplingMethod == SamplingMode.UNIFORM)
        {
            Samples = SamplePointsOnBoundary(Boundary, count);
        }
        else
        {
            Samples = SamplePointsOnBoundaryRandomized(Boundary, count); //TODO delete this at some point
        }

        return Samples;
    }

    //Find Boundary Edges
    private List<Vector3> GetBoundaryLoop(Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Dictionary<(int, int), int> edgeCount = new();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(c, a);
        }

        void AddEdge(int i1, int i2)
        {
            var key = i1 < i2 ? (i1, i2) : (i2, i1);
            if (edgeCount.ContainsKey(key))
                edgeCount[key]++;
            else
                edgeCount[key] = 1;
        }

        // Collect all boundary edges
        List<(int, int)> boundaryEdges = new();
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value == 1)
                boundaryEdges.Add(kvp.Key);
        }

        // Convert to vertex positions
        List<Vector3> boundaryVerts = new();
        foreach (var (i1, i2) in boundaryEdges)
        {
            boundaryVerts.Add(vertices[i1]);
            boundaryVerts.Add(vertices[i2]);
        }

        return boundaryVerts;
    }

    //Order a set of Points counter-clockwise using centroid
    private List<Vector3> OrderBoundaryLoop(List<Vector3> boundaryVerts)
    {
        // Compute centroid
        Vector3 center = Vector3.zero;
        foreach (var v in boundaryVerts) center += v;
        center /= boundaryVerts.Count;

        // Sort by angle around centroid (on XZ plane)
        boundaryVerts.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(a.z - center.z, a.x - center.x);
            float angleB = Mathf.Atan2(b.z - center.z, b.x - center.x);
            return angleA.CompareTo(angleB);
        });

        return boundaryVerts;
    }

    //Compute Perimeter Length (Here all the points are sorted clockwise)
    private float ComputePerimeterLength(List<Vector3> loop)
    {
        float total = 0f;
        for (int i = 0; i < loop.Count; i++)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];
            total += Vector3.Distance(a, b);
        }
        return total;
    }

    private List<Vector3> SamplePointsOnBoundary(List<Vector3> loop, int N, AxisCut axis = AxisCut.Y)
    {
        if (loop == null || loop.Count < 2 || N <= 0)
            return new List<Vector3>();

        // Step 1: Order loop CCW first (if not already)
        List<Vector3> orderedLoop = OrderBoundaryLoop(loop);

        // Step 2: Compute perimeter & step size
        float total = ComputePerimeterLength(orderedLoop);
        float step = total / N;

        // Step 3: Find the vertex closest to +X direction
        int startIndex = FindStartIndexByAxis(orderedLoop, axis);
        orderedLoop = RotateList(orderedLoop, startIndex);

        // Step 4: Sample points
        List<Vector3> sampled = new();
        float distAccum = 0f;
        float nextTarget = 0f;
        int i = 0;

        while (sampled.Count < N)
        {
            Vector3 a = orderedLoop[i];
            Vector3 b = orderedLoop[(i + 1) % orderedLoop.Count];
            float segLen = Vector3.Distance(a, b);

            if (nextTarget <= distAccum + segLen)
            {
                if (segLen < 1e-6f)
                {
                    sampled.Add(a);
                    nextTarget += step;
                    continue;
                }

                float t = Mathf.Clamp01((nextTarget - distAccum) / segLen);
                sampled.Add(Vector3.Lerp(a, b, t));
                nextTarget += step;
            }
            else
            {
                distAccum += segLen;
                i = (i + 1) % orderedLoop.Count;
            }
        }

        return sampled;
    }

    public List<Vector3> SamplePointsOnBoundaryRandomized(List<Vector3> loop, int N, AxisCut axis = AxisCut.Y, float randomFactor = 0.05f)
    {
        if (loop == null || loop.Count < 2 || N <= 0)
            return new List<Vector3>();

        // Order counter-clockwise
        List<Vector3> orderedLoop = OrderBoundaryLoop(loop);

        // Align to +X direction for consistent start
        int startIndex = FindStartIndexByAxis(orderedLoop, axis);
        orderedLoop = RotateList(orderedLoop, startIndex);

        // Compute total perimeter
        float total = ComputePerimeterLength(orderedLoop);
        float avgStep = total / N;

        // Sample with slight randomness in spacing
        List<Vector3> sampled = new();
        float distAccum = 0f;
        float nextTarget = 0f;
        int i = 0;

        System.Random rng = new System.Random();

        while (sampled.Count < N)
        {
            Vector3 a = orderedLoop[i];
            Vector3 b = orderedLoop[(i + 1) % orderedLoop.Count];
            float segLen = Vector3.Distance(a, b);

            if (nextTarget <= distAccum + segLen)
            {
                if (segLen < 1e-6f)
                {
                    sampled.Add(a);
                    nextTarget += avgStep;
                    continue;
                }

                float randomOffset = 1f + ((float)rng.NextDouble() * 2f - 1f) * randomFactor;
                float step = avgStep * randomOffset;
                float t = Mathf.Clamp01((nextTarget - distAccum) / segLen);
                sampled.Add(Vector3.Lerp(a, b, t));
                nextTarget += step;
            }
            else
            {
                distAccum += segLen;
                i = (i + 1) % orderedLoop.Count;
            }
        }

        return sampled;
    }

    //Methods used for grabbers and final positions to start from same angular point
    private static int FindStartIndexByAxis(List<Vector3> points, AxisCut axis)
    {
        if (points == null || points.Count == 0)
            return 0;

        Vector3 center = Vector3.zero;
        foreach (var p in points)
            center += p;
        center /= points.Count;

        float minAngle = float.MaxValue;
        int startIndex = 0;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 pos = points[i] - center;
            float angle = 0f;

            switch (axis)
            {
                case AxisCut.Y:
                    angle = Mathf.Atan2(pos.z, pos.x);
                    break;
                case AxisCut.Z:
                    angle = Mathf.Atan2(pos.y, pos.x);
                    break;
                case AxisCut.X:
                    angle = Mathf.Atan2(pos.z, pos.y);
                    break;
            }

            float absAngle = Mathf.Abs(angle);
            if (absAngle < minAngle)
            {
                minAngle = absAngle;
                startIndex = i;
            }
        }

        return startIndex;
    }

    private static List<T> RotateList<T>(List<T> list, int startIndex)
    {
        if (list == null || list.Count == 0)
            return list;

        startIndex = (startIndex % list.Count + list.Count) % list.Count;
        List<T> rotated = new List<T>();
        rotated.AddRange(list.GetRange(startIndex, list.Count - startIndex));
        rotated.AddRange(list.GetRange(0, startIndex));
        return rotated;
    }

    private List<Vector3> BezierInterpolateSlices(List<Vector3> A, List<Vector3> B, float t, float bezierStrength)
    {
        int count = A.Count;
        List<Vector3> result = new List<Vector3>(count);

        // Build the control points inner Slice
        List<Vector3> C = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 mid = (A[i] + B[i]) * 0.5f;
            Vector3 dir = (B[i] - A[i]).normalized;
            C.Add(mid + bezierStrength * dir);
        }

        // Quadratic Bezier interpolation
        for (int i = 0; i < count; i++)
        {
            float u = 1f - t;
            Vector3 P =
                u * u * A[i] +
                2f * u * t * C[i] +
                t * t * B[i];

            result.Add(P);
        }

        return result;
    }

    private float ComputeBezierStrength(Vector3 lowerSlicePos, Vector3 upperSlicePos)
    {
        float baseDist = Vector3.Distance(lowerSlicePos, upperSlicePos);
        return baseDist * 0.30f; //Can change strength to see different results
    }

    // Return world-space positions of grabbers in the same order as the list
    private static List<Vector3> GetGrabberPositions(List<GameObject> grabbers)
    {
        var pos = new List<Vector3>(grabbers.Count);
        foreach (var g in grabbers)
            pos.Add(g.transform.position);
        return pos;
    }

    // Find the index in 'candidates' whose angle is closest to the reference angle (in radians)
    private static int FindClosestAngleIndex(List<Vector3> candidates, Vector3 referencePoint, AxisCut axis)
    {
        if (candidates == null || candidates.Count == 0) return 0;

        // center for candidates
        Vector3 center = Vector3.zero;
        foreach (var p in candidates) center += p;
        center /= candidates.Count;

        Vector3 refDir = referencePoint - center;
        float refAngle = GetAngleOnPlane(refDir, axis);

        int bestIdx = 0;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            float ang = GetAngleOnPlane(candidates[i] - center, axis);
            float diff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, refAngle * Mathf.Rad2Deg)); // degrees
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    // Rotate samples so their start corresponds to the grabbers' start position
    private static void AlignSamplesToGrabbers(List<Vector3> samples, List<GameObject> grabbers, AxisCut axis)
    {
        if (samples == null || grabbers == null || samples.Count == 0 || grabbers.Count == 0) return;

        // compute grabber positions & centroid
        var gPos = GetGrabberPositions(grabbers);

        // find grabber start point (index 0 after sorting)
        Vector3 grabberRef = gPos[0];

        // find index in samples closest to grabberRef angle
        int sampleMatch = FindClosestAngleIndex(samples, grabberRef, axis);

        // rotate samples so that sampleMatch becomes index 0
        var rotated = RotateList(samples, sampleMatch);
        samples.Clear();
        samples.AddRange(rotated);
    }

    /// Smooth destinations across slices using Laplacian smoothing per-corresponding-vertex.
    /// `sliceDestinations` is a reference to shaper.SliceGrabbers[i].Destinations for all slices (must be filled).
    private void SmoothSliceDestinations(int iterations = 4, float lambda = 0.5f, bool preserveEndpoints = true)
    {
        int sliceCount = shaper.SliceGrabbers.Count;
        if (sliceCount == 0) return;

        // Build jagged array of destinations. Some slices might be empty (no grabbers) - skip them.
        // We'll only smooth slices that have Destinations and the same number of points.
        // Find a canonical count from the first non-empty slice.
        int canonicalCount = -1;
        for (int s = 0; s < sliceCount; s++)
        {
            var d = shaper.SliceGrabbers[s].Destinations;
            if (d != null && d.Count > 0)
            {
                canonicalCount = d.Count;
                break;
            }
        }
        if (canonicalCount <= 0) return;

        // Cache a 2D array: dest[s][v]
        var dest = new List<List<Vector3>>(sliceCount);
        for (int s = 0; s < sliceCount; s++)
        {
            var D = shaper.SliceGrabbers[s].Destinations;
            if (D != null && D.Count == canonicalCount)
                dest.Add(new List<Vector3>(D)); // copy
            else
                dest.Add(null); // mark as invalid / skip
        }

        // For smoothing, we need contiguous sections. We'll smooth across contiguous regions,
        // but keep slices that are null (invalid) out.
        // Optionally preserve endpoints of each contiguous region.
        for (int iter = 0; iter < iterations; iter++)
        {
            // We will compute newDest as copy and then overwrite dest (Jacobi style).
            var newDest = new List<List<Vector3>>(sliceCount);
            for (int s = 0; s < sliceCount; s++)
                newDest.Add(dest[s] == null ? null : new List<Vector3>(dest[s]));

            int sIdx = 0;
            while (sIdx < sliceCount)
            {
                // skip invalid slices
                if (dest[sIdx] == null) { sIdx++; continue; }

                // find contiguous block [a..b]
                int a = sIdx;
                int b = a;
                while (b + 1 < sliceCount && dest[b + 1] != null) b++;

                int blockLen = b - a + 1;
                if (blockLen >= 3)
                {
                    // For each vertex index v (0..canonicalCount-1), smooth along a..b
                    for (int v = 0; v < canonicalCount; v++)
                    {
                        // Optionally preserve endpoints
                        int start = preserveEndpoints ? a + 1 : a;
                        int end = preserveEndpoints ? b - 1 : b;

                        for (int s = start; s <= end; s++)
                        {
                            // Laplacian: average of neighbors
                            Vector3 left = dest[s - 1][v];
                            Vector3 right = dest[s + 1][v];
                            Vector3 lap = (left + right) * 0.5f - dest[s][v];

                            newDest[s][v] = dest[s][v] + lambda * lap;
                        }
                    }
                }
                // move to next block
                sIdx = b + 1;
            }

            // commit newDest -> dest
            for (int s = 0; s < sliceCount; s++)
                if (newDest[s] != null)
                    dest[s] = newDest[s];
        }

        // Write back into shaper.SliceGrabbers
        for (int s = 0; s < sliceCount; s++)
        {
            if (dest[s] != null)
                shaper.SliceGrabbers[s].Destinations = new List<Vector3>(dest[s]);
        }
    }

    private void SplitOuterInner(SliceData slice, AxisCut axis){
        slice.OuterGrabbers.Clear();
        slice.InnerGrabbers.Clear();

        if (slice.Grabbers.Count < 4)
        {
            slice.OuterGrabbers.AddRange(slice.Grabbers);
            return;
        }

        // Convert grabbers → 2D plane for hull
        List<(GameObject obj, Vector2 p)> pts = new();
        foreach (var g in slice.Grabbers)
        {
            Vector3 wp = g.transform.position;
            pts.Add((g, ProjectTo2D(wp, axis)));
        }

        // Compute convex hull in 2D
        var hull = ComputeConvexHull(pts);

        HashSet<GameObject> hullSet = new(hull);

        foreach (var (obj, p) in pts)
        {
            if (hullSet.Contains(obj)) slice.OuterGrabbers.Add(obj);
            else slice.InnerGrabbers.Add(obj);
        }
        //Keep Only Outer Grabbers inside slice.Grabbers, so that smoothing works on edge slices too
        slice.Grabbers.RemoveAll(item => slice.InnerGrabbers.Contains(item));
    }

    // Projects 3D point onto 2D plane depending on slicing axis
    private Vector2 ProjectTo2D(Vector3 p, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.Y => new Vector2(p.x, p.z),
            AxisCut.X => new Vector2(p.y, p.z),
            AxisCut.Z => new Vector2(p.x, p.y),
            _ => new Vector2(p.x, p.z)
        };
    }

    // Graham Scan convex hull
    private List<GameObject> ComputeConvexHull(List<(GameObject obj, Vector2 p)> pts)
    {
        // Sort by x then y
        pts.Sort((a, b) =>
        {
            int c = a.p.x.CompareTo(b.p.x);
            return c != 0 ? c : a.p.y.CompareTo(b.p.y);
        });

        List<(GameObject obj, Vector2 p)> hull = new();

        // Lower hull
        foreach (var pt in pts)
        {
            while (hull.Count >= 2 &&
                 Cross(hull[hull.Count - 2].p, hull[hull.Count - 1].p, pt.p) <= 0)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(pt);
        }

        // Upper hull
        int lowerCount = hull.Count;
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var pt = pts[i];
            while (hull.Count > lowerCount &&
                  Cross(hull[hull.Count - 2].p, hull[hull.Count - 1].p, pt.p) <= 0)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(pt);
        }

        hull.RemoveAt(hull.Count - 1);

        List<GameObject> result = new();
        foreach (var h in hull) result.Add(h.obj);
        return result;
    }

    private float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    // Project each point to the slicing plane by clamping the axis component to the given planeCoord.
    // This returns a new list (doesn't mutate original).
    private static List<Vector3> ProjectToSlicePlane(List<Vector3> pts, AxisCut axis, float planeCoord)
    {
        var outList = new List<Vector3>(pts.Count);
        foreach (var p in pts)
        {
            Vector3 q = p;
            switch (axis)
            {
                case AxisCut.X: q.x = planeCoord; break;
                case AxisCut.Y: q.y = planeCoord; break;
                case AxisCut.Z: q.z = planeCoord; break;
            }
            outList.Add(q);
        }
        return outList;
    }

    // Compute centroid scalar along axis for a list of world points (used as plane coordinate).
    private static float ComputeAxisPlaneCoord(List<Vector3> pts, AxisCut axis)
    {
        if (pts == null || pts.Count == 0) return 0f;
        float sum = 0f;
        switch (axis)
        {
            case AxisCut.X:
                foreach (var p in pts) sum += p.x;
                return sum / pts.Count;
            case AxisCut.Y:
                foreach (var p in pts) sum += p.y;
                return sum / pts.Count;
            case AxisCut.Z:
                foreach (var p in pts) sum += p.z;
                return sum / pts.Count;
            default: return 0f;
        }
    }

    // Called after outer Grabbers Final positions are calculated
    private void DeformInnerGrabbersForEdgeSlice(SliceData slice)
    {
        slice.InnerDestinations = new List<Vector3>();

        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            var pg = slice.InnerGrabbers[i].GetComponent<ParticleGrab>();

            Vector3 B_Coords;
            B_Coords = pg.GetBarycentricCoordinates(); // Barycentric Coordinates
            Debug.Assert( pg.TriangleIndices != null );
            Debug.Assert(pg.TriangleIndices.Count == 3);
            Vector3 A = slice.Destinations[pg.TriangleIndices[0]];
            Vector3 B = slice.Destinations[pg.TriangleIndices[1]];
            Vector3 C = slice.Destinations[pg.TriangleIndices[2]];

            // Q = x*A + y*B + z*C
            Vector3 InnerPos = (B_Coords.x * A) + (B_Coords.y * B) + (B_Coords.z * C);
            slice.InnerDestinations.Add(InnerPos);
        }
    }
}
