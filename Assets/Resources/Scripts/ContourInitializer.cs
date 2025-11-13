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

    public override void InitializeSlices()
    {
        InitializationV3();
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

        //instantiate samples and grabbers

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
                EmptyContours.Add(s);
                Interpolate = true;
            }
            else //Full Slice
            {
                //This should go by the end
                lastfull = s;

                // Use counter-clockwise sorting to grabbers
                var SortedIndices = GetCounterClockwiseOrderIndices(grabbers, axis);

                // Get ordered boundary and sampled positions
                List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
                List<Vector3> Samples = SamplePoints(Boundary, slice.Grabbers.Count);

                // --- 1. Flip samples if normals disagree ---
                Vector3 grabberNormal = GetPlaneNormal(axis);
                Vector3 sampleNormal = ComputePolygonNormal(Samples);

                if (Vector3.Dot(grabberNormal, sampleNormal) < 0f)
                {
                    Samples.Reverse();
                }

                // --- 2. Align start angles between grabbers and samples ---
                Vector3 grabberCenter = GetCentroid(grabbers);
                Vector3 sampleCenter = Vector3.zero;
                foreach (var p in Samples) sampleCenter += p;
                sampleCenter /= Samples.Count;

                // Compute angular offset
                float grabberStartAngle = GetAngleOnPlane(grabbers[SortedIndices[0]].transform.position - grabberCenter, axis);
                float sampleStartAngle = GetAngleOnPlane(Samples[0] - sampleCenter, axis);
                float angleOffset = grabberStartAngle - sampleStartAngle;

                // Rotate samples to match grabber start angle
                Samples = RotateByAngle(Samples, sampleCenter, axis, angleOffset);

                //To Delete
                var par = new GameObject("Grabbers Debug");
                var par2 = new GameObject("Finals Debug");
                for (int i = 0; i < SortedIndices.Count; i++)
                {
                    var inst = Instantiate(DebugObject, par.transform);
                    inst.name = "Initial " + SortedIndices[i];
                    inst.transform.position = grabbers[SortedIndices[i]].transform.position;
                }

                // Assign destinations
                for (int i = 0; i < grabbers.Count; i++)
                {
                    Vector3 grabberPos = grabbers[i].transform.position; //SortedIndices
                    Debug.Log("Sorted Index: " + SortedIndices[i]);
                    Vector3 target = Samples[SortedIndices[i]];

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

    private void InitializationV3()
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
        List<float> contourAxisPositions = new();
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

        // Sort contours along the axis
        SortContoursByAxis(ref ContourSlices, ref contourAxisPositions);

        List<int> EmptyContours = new();
        int lastFull = 0;
        bool Interpolate = false;

        // Iterate through all slices
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

            // 1. Sort grabbers CCW directly
            SortGrabbersCounterClockwiseInPlace(slice.Grabbers, axis);

            // 2. Get ordered boundary and sample same number of points
            List<Vector3> Boundary = GetOrderedBoundaryWorld(ContourSlices[s]);
            List<Vector3> Samples = SamplePoints(Boundary, slice.Grabbers.Count);

            // 3. Flip samples if normals disagree
            Vector3 grabberNormal = GetPlaneNormal(axis);
            Vector3 sampleNormal = ComputePolygonNormal(Samples);
            if (Vector3.Dot(grabberNormal, sampleNormal) < 0f)
                Samples.Reverse();

            // 4. Ensure samples start at the same angle reference (+X)
            Samples = SortPointsCounterClockwise(Samples, axis);

            //To Delete
            var par = new GameObject("Grabbers Debug");
            var par2 = new GameObject("Finals Debug");
            for (int i = 0; i < grabbers.Count; i++)
            {
                var inst = Instantiate(DebugObject, par.transform);
                inst.name = "Initial " + i;
                inst.transform.position = grabbers[i].transform.position;
            }

            //5. Assign destinations directly, 1-to-1
            for (int i = 0; i < grabbers.Count; i++)
            {
                Vector3 grabberPos = grabbers[i].transform.position;
                Vector3 target = Samples[i];

                // Lock slice axis
                switch (axis)
                {
                    case AxisCut.X: target.x = grabberPos.x; break;
                    case AxisCut.Y: target.y = grabberPos.y; break;
                    case AxisCut.Z: target.z = grabberPos.z; break;
                }
                //To delete
                var inst = Instantiate(DebugObject, par2.transform);
                inst.name = "Final " + i;
                inst.transform.position = target;

                slice.Destinations.Add(target);
            }

            // --- Optional interpolation for empty slices ---
            if (Interpolate)
            {
                Debug.Assert(EmptyContours.Count != 0);
                // Interpolation logic will go here
            }
        }
    }


    //Methods for sorting samples/grabbers

    private void SortGrabbersCounterClockwiseInPlace(List<GameObject> grabbers, AxisCut axis)
    {
        if (grabbers == null || grabbers.Count < 3)
            return;

        Vector3 center = GetCentroid(grabbers);

        grabbers.Sort((a, b) =>
        {
            Vector3 pa = a.transform.position - center;
            Vector3 pb = b.transform.position - center;

            float angleA = GetAngleOnPlane(pa, axis);
            float angleB = GetAngleOnPlane(pb, axis);

            return angleA.CompareTo(angleB);
        });
    }

    private List<Vector3> SortPointsCounterClockwise(List<Vector3> points, AxisCut axis)
    {
        if (points == null || points.Count < 3)
            return points;

        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        return points.OrderBy(p => GetAngleOnPlane(p - center, axis)).ToList();
    }


    /*
    private List<GameObject> SortGrabbersCounterClockwise(ref List<GameObject> grabbers, AxisCut axis)
    {
        if (grabbers == null || grabbers.Count < 3)
            return grabbers;

        Vector3 center = GetCentroid(grabbers);
        return grabbers.OrderBy(g =>
        {
            Vector3 pos = g.transform.position - center;
            return GetAngleOnPlane(pos, axis);
        }).ToList();
    }

    private List<Vector3> SortPointsCounterClockwise(List<Vector3> points, AxisCut axis)
    {
        if (points == null || points.Count < 3)
            return points;

        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        center /= points.Count;

        return points.OrderBy(p => GetAngleOnPlane(p - center, axis)).ToList();
    }*/



    private void AlignSampleStartWithGrabbers(
    ref List<Vector3> Samples,
    List<GameObject> grabbers,
    List<int> sortedGrabberIndices,
    AxisCut axis)
    {
        if (Samples == null || Samples.Count < 2 || grabbers == null || grabbers.Count < 2)
            return;

        // Compute centers
        Vector3 grabberCenter = Vector3.zero;
        foreach (var g in grabbers)
            grabberCenter += g.transform.position;
        grabberCenter /= grabbers.Count;

        Vector3 sampleCenter = Vector3.zero;
        foreach (var s in Samples)
            sampleCenter += s;
        sampleCenter /= Samples.Count;

        // First grabber reference angle (after sorting)
        Vector3 firstGrabberPos = grabbers[sortedGrabberIndices[0]].transform.position - grabberCenter;
        float grabberAngle = ComputeAngle(firstGrabberPos, axis);

        // First sample angle
        Vector3 firstSamplePos = Samples[0] - sampleCenter;
        float sampleAngle = ComputeAngle(firstSamplePos, axis);

        // Compute angular offset
        float deltaAngle = grabberAngle - sampleAngle;

        // Rotate sample list by closest angle match
        int rotationSteps = Mathf.RoundToInt((deltaAngle / (2 * Mathf.PI)) * Samples.Count);
        rotationSteps = (rotationSteps % Samples.Count + Samples.Count) % Samples.Count;
        Samples = RotateList(Samples, rotationSteps);
    }

    private float ComputeAngle(Vector3 pos, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.Z => Mathf.Atan2(pos.y, pos.x),
            AxisCut.Y => Mathf.Atan2(pos.z, pos.x),
            AxisCut.X => Mathf.Atan2(pos.z, pos.y),
            _ => 0f
        };
    }


    private Vector3 GetCentroid(List<GameObject> objects)
    {
        Vector3 c = Vector3.zero;
        foreach (var o in objects)
            c += o.transform.position;
        return c / objects.Count;
    }

    private float GetAngleOnPlane(Vector3 pos, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.Y => Mathf.Atan2(pos.z, pos.x),
            AxisCut.Z => Mathf.Atan2(pos.y, pos.x),
            AxisCut.X => Mathf.Atan2(pos.z, pos.y),
            _ => 0f
        };
    }

    /*
    private Vector3 GetCentroid(List<GameObject> objs)
    {
        Vector3 center = Vector3.zero;
        foreach (var o in objs)
            center += o.transform.position;
        return center / objs.Count;
    }

    private float GetAngleOnPlane(Vector3 pos, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.Z => Mathf.Atan2(pos.y, pos.x),
            AxisCut.Y => Mathf.Atan2(pos.z, pos.x),
            AxisCut.X => Mathf.Atan2(pos.z, pos.y),
            _ => 0f
        };
    }*/


    // Rotates a list of points around the centroid by a given angle (on the slicing plane)
    private List<Vector3> RotateByAngle(List<Vector3> points, Vector3 center, AxisCut axis, float angle)
    {
        Quaternion rot = axis switch
        {
            AxisCut.X => Quaternion.AngleAxis(Mathf.Rad2Deg * angle, Vector3.right),
            AxisCut.Y => Quaternion.AngleAxis(Mathf.Rad2Deg * angle, Vector3.up),
            AxisCut.Z => Quaternion.AngleAxis(Mathf.Rad2Deg * angle, Vector3.forward),
            _ => Quaternion.identity
        };

        List<Vector3> rotated = new();
        foreach (var p in points)
            rotated.Add(center + rot * (p - center));
        return rotated;
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
    //

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
