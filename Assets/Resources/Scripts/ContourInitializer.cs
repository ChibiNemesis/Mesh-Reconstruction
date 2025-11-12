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

    private enum SamplingMode {UNIFORM, RANDOMIZED }

    //Sampling Method for Sampling across contour's perimeter
    [SerializeField]
    SamplingMode SamplingMethod = SamplingMode.UNIFORM;

    [SerializeField]
    [Range(0f, 0.1f)]
    float SamplingFactor = 0.05f;


    //Will be deleted
    [SerializeField]
    GameObject DebugObject;

    private void Start()
    {
        ContourSlices = new List<MeshFilter>();
        ContourTransform = Contour.transform;

        for (int c = 0; c < Contour.transform.childCount; c++)
        {
            ContourSlices.Add(Contour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>());
        }
    }
    //Idea #1: Divide each contour using angle on axis (current)
    //Idea #2: Use each slice as intermediate and map based (like Texture mapping)
    public override void InitializeSlices()
    {
        InitializationV2();
    }


    private void InitializationV1()
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

        // Sort contours by position, in case they are not sorted
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
            float t = (Mathf.Abs(upperPos - lowerPos) < 1e-6f) ? 0f : Mathf.InverseLerp(lowerPos, upperPos, slicePos);

            // Generate sampled contour points
            List<Vector3> lowerBoundary = GetOrderedBoundaryWorld(lowerContour);
            List<Vector3> upperBoundary = GetOrderedBoundaryWorld(upperContour);

            // Interpolate between corresponding points
            List<Vector3> interpolatedContour = InterpolateContours(lowerBoundary, upperBoundary, t, grabbers.Count);

            //Use counter-clockwise sorting to grabbers, and assign to grabbers based on sorted indices
            var SortedIndices = GetCounterClockwiseOrderIndices(grabbers, axis);

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

    private void InitializationV2()
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

        // Sort contours by position, in case they are not sorted
        SortContoursByAxis(ref ContourSlices, ref contourAxisPositions);

        List<int> EmptyContours = new List<int>();
        int lastfull = 0;
        //int EmptyCount = 0;
        bool Interpolate = false;

        for (int s = 0; s < sliceCount; s++)
        {
            SliceData slice = shaper.SliceGrabbers[s];
            slice.Destinations = new List<Vector3>();
            List<GameObject> grabbers = slice.Grabbers;
            if (grabbers.Count == 0)
                continue;
            // -- Each slice is either full or empty
            // -- If slice is not empty -> Calculate new positions and sort them counter-clockwise
            // -- If slice is empty continue until you find a full slice or you reach the last slice (keep track of the number of previous)
            // -- once another full slice is found calculate the final positions for its grabbers
            // -- Then, interpolate each empty slice starting from the first one 
            
            if (ContourSlices[s] == null) //Empty slice
            {
                //EmptyCount++;
                EmptyContours.Add(s);
                Interpolate = true;
            }
            else //Full Slice
            {
                lastfull = s;

                //Find final positions for current slice first
                //First, acquire Boundary points
                List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
                var Samples = SamplePoints(Boundary, slice.Grabbers.Count);
                Samples = RotateList(Samples, FindStartIndexByAxis(Samples, axis));
                Samples.Reverse();

                //Use counter-clockwise sorting to grabbers, and assign to grabbers based on sorted indices
                var SortedIndices = GetCounterClockwiseOrderIndices(grabbers, axis);
                //To Delete
                var par = new GameObject("Grabbers Debug");
                var par2 = new GameObject("Finals Debug");
                for (int i = 0; i < SortedIndices.Count; i++)
                {
                    var inst = Instantiate(DebugObject, par.transform);
                    inst.name = "Initial " + i;
                    inst.transform.position = grabbers[SortedIndices[i]].transform.position;
                }

                // Assign destinations
                for (int i = 0; i < grabbers.Count; i++)
                {
                    Vector3 target = Samples[i];

                    Vector3 grabberPos = grabbers[SortedIndices[i]].transform.position;

                    // Lock the slicing axis
                    switch (axis)
                    {
                        case AxisCut.X: target.x = grabberPos.x; break;
                        case AxisCut.Y: target.y = grabberPos.y; break;
                        case AxisCut.Z: target.z = grabberPos.z; break;
                    }

                    //To delete
                    var inst2 = Instantiate(DebugObject, par2.transform);
                    inst2.name = "Final " + i;
                    inst2.transform.position = target;

                    slice.Destinations.Add(target);
                }

                if (Interpolate)
                {
                    Debug.Assert(EmptyContours.Count != 0);
                    //Interpolate for empty slices ----
                }
            }
            
        }
    }

    //Method for returning the indices of the final position counter-clockwise
    public static List<int> GetCounterClockwiseOrderIndicesOld(List<GameObject> objects, AxisCut axis = AxisCut.Y) // PlaneAxis axis = PlaneAxis.XZ
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

    public static List<int> GetCounterClockwiseOrderIndices(List<GameObject> objects, AxisCut axis = AxisCut.Y)
    {
        List<int> indices = new List<int>();
        if (objects == null || objects.Count < 3)
        {
            for (int i = 0; i < (objects?.Count ?? 0); i++)
                indices.Add(i);
            return indices;
        }

        // Step 1: Compute centroid
        Vector3 center = Vector3.zero;
        foreach (var obj in objects)
            center += obj.transform.position;
        center /= objects.Count;

        // Step 2: Compute angles for each object
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

        // Step 3: Sort counter-clockwise
        indexedAngles.Sort((a, b) => a.angle.CompareTo(b.angle));

        // Step 4: Find the one closest to +X (angle ~ 0)
        int startIndex = 0;
        float minAbs = float.MaxValue;
        for (int i = 0; i < indexedAngles.Count; i++)
        {
            float absAngle = Mathf.Abs(indexedAngles[i].angle);
            if (absAngle < minAbs)
            {
                minAbs = absAngle;
                startIndex = i;
            }
        }

        // Step 5: Rotate so list starts from +X direction
        for (int i = 0; i < indexedAngles.Count; i++)
        {
            int idx = (i + startIndex) % indexedAngles.Count;
            indices.Add(indexedAngles[idx].index);
        }

        return indices;
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

        List<Vector3> lowerSample;
        List<Vector3> upperSample;

        if (SamplingMethod == SamplingMode.UNIFORM)
        {
            lowerSample = SamplePointsOnBoundary(lower, count);
            upperSample = SamplePointsOnBoundary(upper, count);
        }
        else
        {
            lowerSample = SamplePointsOnBoundaryRandomized(lower, count); //initialize correct
            upperSample = SamplePointsOnBoundaryRandomized(upper, count);
        }


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

    //Sample N evenly spaced sorted points on that perimeter
    private List<Vector3> SamplePointsOnBoundaryOld(List<Vector3> loop, int N)
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


    //Sample N non-uniform spaced sorted points on that perimeter
    public List<Vector3> SamplePointsOnBoundaryRandomizedOld(List<Vector3> loop, int N, float randomFactor = 0.05f)
    {
        if (loop == null || loop.Count < 2 || N <= 0)
            return new List<Vector3>();

        List<Vector3> sampled = new();
        float total = ComputePerimeterLength(loop);
        float avgStep = total / N;

        float distAccum = 0f;
        float nextTarget = 0f;
        int i = 0;

        System.Random rng = new System.Random();

        while (sampled.Count < N)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];
            float segLen = Vector3.Distance(a, b);

            if (nextTarget <= distAccum + segLen)
            {
                if (segLen < 1e-6f)
                {
                    sampled.Add(a);
                    nextTarget += avgStep;
                    continue;
                }

                // Introduce small randomized perturbation
                float randomOffset = 1f + ((float)rng.NextDouble() * 2f - 1f) * randomFactor;
                float step = avgStep * randomOffset;

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
