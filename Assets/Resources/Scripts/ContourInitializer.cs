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

    private Transform ContourTransform;

    public List<MeshFilter> ContourSlices;

    [SerializeField]
    [Range(0f, 0.1f)]
    float SamplingFactor = 0.05f;

    private void Start()
    {
        ContourSlices = new List<MeshFilter>();
        ContourTransform = Contour.transform;

        for (int c = 0; c < Contour.transform.childCount; c++)
        {
            var mesh = Contour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>();
            ContourSlices.Add(mesh);
        }
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

        List<int> EmptyContours = new List<int>();
        int lastFull = 0;
        bool Interpolate = false;

        for (int s = 0; s < sliceCount; s++)
        {
            SliceData slice = shaper.SliceGrabbers[s];
            slice.Destinations = new List<Vector3>();
            List<GameObject> grabbers = slice.Grabbers;
            if (grabbers.Count == 0)
                continue;

            if (ContourSlices[s] == null)
            {
                EmptyContours.Add(s);
                Interpolate = true;
                continue;
            }

            lastFull = s;

            // --- Sort grabbers CCW using global angular reference ---
            SortGrabbersCounterClockwiseInPlace(grabbers, axis);

            // --- Get contour boundary and sample points ---
            List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
            List<Vector3> Samples = SamplePoints(Boundary, grabbers.Count);

            // --- Align normals: ensure samples face same direction as grabbers ---
            Vector3 grabberNormal = ComputePolygonNormal(grabbers.Select(g => g.transform.position).ToList());
            Vector3 sampleNormal = ComputePolygonNormal(Samples);
            if (Vector3.Dot(grabberNormal, sampleNormal) < 0f)
            {
                Samples.Reverse();
            }

            // --- Sort samples CCW with global reference to match grabbers ---
            Samples = SortPointsCounterClockwise(Samples, axis);

            // --- Assign destinations 1-to-1 ---
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

            // --- Interpolate for missing slices ---
            if (Interpolate)
            {
                Debug.Assert(EmptyContours.Count != 0);

                var LastPos = shaper.SliceGrabbers[lastFull].Destinations;
                var CurrPos = slice.Destinations;

                for (int idx = 0; idx < EmptyContours.Count; idx++)
                {
                    float t = (float)(idx + 1) / (EmptyContours.Count + 1);
                    var interpolated = InterpolateContours(LastPos, CurrPos, t, shaper.SliceGrabbers[EmptyContours[idx]].Grabbers.Count);
                    shaper.SliceGrabbers[EmptyContours[idx]].Destinations.AddRange(interpolated);
                }

                EmptyContours.Clear();
                Interpolate = false;
            }
        }
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

    /// Computes the angle of a vector projected on the given plane (axis) relative to +X direction.
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
            Samples = SamplePointsOnBoundaryRandomized(Boundary, count); // initializeCorrect
        }

        return Samples;
    }

    private List<Vector3> InterpolateContours(List<Vector3> lower, List<Vector3> upper, float t, int count)
    {

        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            Vector3 interpolated = Vector3.Lerp(lower[i], upper[i], t);
            result.Add(interpolated);
        }
        return result;
    }

    //Find Boundary Edges (those used only by one triangle)
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

        // Optional: order them in loop (see step 2)
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

        // 1️⃣ Order counter-clockwise
        List<Vector3> orderedLoop = OrderBoundaryLoop(loop);

        // 2️⃣ Align to +X direction for consistent start
        int startIndex = FindStartIndexByAxis(orderedLoop, axis);
        orderedLoop = RotateList(orderedLoop, startIndex);

        // 3️⃣ Compute total perimeter
        float total = ComputePerimeterLength(orderedLoop);
        float avgStep = total / N;

        // 4️⃣ Sample with slight randomness in spacing
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

}
