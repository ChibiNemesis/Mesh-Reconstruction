using System.Collections;
using System.Collections.Generic;
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

    private void Start()
    {
        ContourSlices = new List<MeshFilter>();
        ContourTransform = Contour.transform;

        for (int c = 0; c < Contour.transform.childCount; c++)
        {
            ContourSlices.Add(Contour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>());
        }
    }

    public override void InitializeSlices()
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

        // Compute normalized positions along slicing axis for contours
        List<float> contourAxisPositions = new List<float>();
        foreach (var mf in ContourSlices)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 pos = mf.transform.position;
            float coord = axis switch
            {
                AxisCut.X => pos.x + b.center.x,
                AxisCut.Y => pos.y + b.center.y,
                AxisCut.Z => pos.z + b.center.z,
                _ => 0f
            };
            contourAxisPositions.Add(coord);
        }

        // Sort contours by position (important for interpolation)
        SortContoursByAxis(ref ContourSlices, ref contourAxisPositions);

        for (int s = 0; s < sliceCount; s++)
        {
            SliceData slice = shaper.SliceGrabbers[s];
            slice.Destinations = new List<Vector3>();
            List<GameObject> grabbers = slice.Grabbers;
            if (grabbers.Count == 0)
                continue;

            // Compute current slice position on slicing axis
            float slicePos = axis switch
            {
                AxisCut.X => (slice.Min.x + slice.Max.x) * 0.5f,
                AxisCut.Y => (slice.Min.y + slice.Max.y) * 0.5f,
                AxisCut.Z => (slice.Min.z + slice.Max.z) * 0.5f,
                _ => 0f
            };

            // Find nearest contour indices for interpolation
            int lowerIndex = 0;
            int upperIndex = contourCount - 1;

            for (int i = 0; i < contourAxisPositions.Count - 1; i++)
            {
                if (slicePos >= contourAxisPositions[i] && slicePos <= contourAxisPositions[i + 1])
                {
                    lowerIndex = i;
                    upperIndex = i + 1;
                    break;
                }
            }

            MeshFilter lowerContour = ContourSlices[lowerIndex];
            MeshFilter upperContour = ContourSlices[upperIndex];

            float lowerPos = contourAxisPositions[lowerIndex];
            float upperPos = contourAxisPositions[upperIndex];
            //float t = Mathf.InverseLerp(lowerPos, upperPos, slicePos);
            float t = (Mathf.Abs(upperPos - lowerPos) < 1e-6f) ? 0f : Mathf.InverseLerp(lowerPos, upperPos, slicePos);


            // Generate sampled contour points
            List<Vector3> lowerBoundary = GetOrderedBoundaryWorld(lowerContour);
            List<Vector3> upperBoundary = GetOrderedBoundaryWorld(upperContour);

            // Interpolate between corresponding points
            List<Vector3> interpolatedContour = InterpolateContours(lowerBoundary, upperBoundary, t, grabbers.Count);

            //Use counter-clockwise sorting to grabbers, and assign based on
            var SortedIndices = GetCounterClockwiseOrderIndices(grabbers, axis); //this should always be AxisCut.Y


            // Assign destinations
            for (int i = 0; i < grabbers.Count; i++)
            {
                Vector3 target = interpolatedContour[SortedIndices[i]];

                //target = ContourTransform.TransformPoint(target); //Go to world space based on this object's transform
                Vector3 grabberPos = grabbers[i].transform.position;

                // Lock the slicing axis
                switch (axis)
                {
                    case AxisCut.X: target.x = grabberPos.x; break;
                    case AxisCut.Y: target.y = grabberPos.y; break;
                    case AxisCut.Z: target.z = grabberPos.z; break;
                }

                //Sort CounterClockwise
                slice.Destinations.Add(target);
            }
        }
    }

    // Helper functions

    //Method for returning the indices of the final position counter-clockwise
    public static List<int> GetCounterClockwiseOrderIndices(List<GameObject> objects, AxisCut axis = AxisCut.Y) // PlaneAxis axis = PlaneAxis.XZ
    {
        List<int> indices = new List<int>();

        if (objects == null || objects.Count < 3)
        {
            for (int i = 0; i < (objects?.Count ?? 0); i++)
                indices.Add(i);
            return indices;
        }

        // Step 1: Compute centroid in world space
        Vector3 center = Vector3.zero;
        foreach (var obj in objects)
            center += obj.transform.position;
        center /= objects.Count;

        // Step 2: Pair each object with its computed angle
        List<(int index, float angle)> indexedAngles = new List<(int, float)>();

        for (int i = 0; i < objects.Count; i++)
        {
            Vector3 pos = objects[i].transform.position - center;
            float angle = 0f;

            switch (axis)
            {
                case AxisCut.Z:
                    angle = Mathf.Atan2(pos.y, pos.x);
                    break;

                case AxisCut.Y:
                    angle = Mathf.Atan2(pos.z, pos.x);
                    break;

                case AxisCut.X:
                    angle = Mathf.Atan2(pos.z, pos.y);
                    break;
            }

            indexedAngles.Add((i, angle));
        }

        // Step 3: Sort by angle (counter-clockwise)
        indexedAngles.Sort((a, b) => a.angle.CompareTo(b.angle));

        // Step 4: Extract indices in sorted order
        foreach (var entry in indexedAngles)
            indices.Add(entry.index);

        return indices;
    }

    private void SortContoursByAxis(ref List<MeshFilter> contours, ref List<float> positions)
    {
        var combined = new List<(MeshFilter, float)>();
        for (int i = 0; i < contours.Count; i++)
            combined.Add((contours[i], positions[i]));
        combined.Sort((a, b) => a.Item2.CompareTo(b.Item2));

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

    private List<Vector3> InterpolateContours(List<Vector3> lower, List<Vector3> upper, float t, int count)
    {
        List<Vector3> lowerSample = SamplePointsOnBoundary(lower, count);
        List<Vector3> upperSample = SamplePointsOnBoundary(upper, count);

        List<Vector3> result = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            Vector3 interpolated = Vector3.Lerp(lowerSample[i], upperSample[i], t);
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

    //Sample N evenly spaced points on that perimeter (this could be randomized)
    private List<Vector3> SamplePointsOnBoundary(List<Vector3> loop, int N)
    {
        List<Vector3> sampled = new();
        float total = ComputePerimeterLength(loop);
        float step = total / N;

        float distAccum = 0f;
        float nextTarget = 0f;
        int i = 0;

        while (sampled.Count < N)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];
            float segLen = Vector3.Distance(a, b);

            if (nextTarget <= distAccum + segLen)
            {
                //float t = (nextTarget - distAccum) / segLen;
                //sampled.Add(Vector3.Lerp(a, b, t));
                if (segLen < 1e-6f)
                {
                    sampled.Add(a); // skip degenerate edge
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
                i = (i + 1) % loop.Count;
            }
        }

        return sampled;
    }
}
