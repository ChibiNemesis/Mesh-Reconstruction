using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeshComparison
{

    // Normalize
    public static Mesh NormalizeMesh(Mesh mesh, Transform meshTransform)
    {
        Mesh copy = Object.Instantiate(mesh);
        Vector3[] verts = copy.vertices;

        // 1. Transform ALL vertices to World Space first
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = meshTransform.TransformPoint(verts[i]);
        }

        // 2. Calculate Bounds of the WORLD vertices
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < verts.Length; i++)
        {
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }

        // 3. Calculate World Center and Scale
        Vector3 center = (min + max) * 0.5f;
        float maxDimension = (max - min).magnitude;

        // Prevent division by zero
        float scale = maxDimension > 0.00001f ? (1f / maxDimension) : 1f;

        // 4. Normalize (Center at 0,0,0 and Scale to Unit size)
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = (verts[i] - center) * scale;
        }

        copy.vertices = verts;
        copy.RecalculateBounds();
        copy.RecalculateNormals(); // Recalculate normals on the aligned mesh

        return copy;
    }

    public static Mesh NormalizeMeshForEvaluation(Mesh mesh, Transform meshTransform, Vector3 targetCenter)
    {
        Mesh copy = Object.Instantiate(mesh);
        Vector3[] verts = copy.vertices;

        // 1. Transform to World Space
        for (int i = 0; i < verts.Length; i++)
            verts[i] = meshTransform.TransformPoint(verts[i]);

        // 2. Compute Centroid
        Vector3 center = Vector3.zero;
        foreach (var v in verts) center += v;
        center /= verts.Length;

        // 3. Offset to match Target Center (Alignment Only)
        // DO NOT SCALE here. Keep the original size.
        Vector3 offset = targetCenter - center;

        for (int i = 0; i < verts.Length; i++)
            verts[i] += offset;

        copy.vertices = verts;
        copy.RecalculateBounds();
        copy.RecalculateNormals();

        return copy;
    }

    // Surface Sampling
    public static List<Vector3> SampleMeshSurface(Mesh mesh, int sampleCount)
    {
        var tris = mesh.triangles;
        var verts = mesh.vertices;

        List<Vector3> samples = new(sampleCount);
        List<float> cumulativeAreas = new();
        float totalArea = 0f;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            totalArea += area;
            cumulativeAreas.Add(totalArea);
        }

        for (int i = 0; i < sampleCount; i++)
        {
            float r = Random.value * totalArea;
            int triIndex = cumulativeAreas.FindIndex(a => a >= r);
            if (triIndex < 0) triIndex = cumulativeAreas.Count - 1;

            int triStart = triIndex * 3;
            Vector3 a = verts[tris[triStart]];
            Vector3 b = verts[tris[triStart + 1]];
            Vector3 c = verts[tris[triStart + 2]];

            float u = Mathf.Sqrt(Random.value);
            float v = Random.value;
            Vector3 p = a * (1 - u) + b * (u * (1 - v)) + c * (u * v);
            samples.Add(p);
        }

        return samples;
    }

    // Normal Sampling
    public static List<Vector3> SampleMeshNormals(Mesh mesh, List<Vector3> samplePoints)
    {
        List<Vector3> normals = new(samplePoints.Count);
        Vector3[] meshNormals = mesh.normals;
        Vector3[] meshVerts = mesh.vertices;
        int[] tris = mesh.triangles;

        for (int i = 0; i < samplePoints.Count; i++)
        {
            Vector3 p = samplePoints[i];
            int nearest = FindNearestVertexIndex(p, meshVerts);
            normals.Add(meshNormals[nearest]);
        }

        return normals;
    }

    private static int FindNearestVertexIndex(Vector3 p, Vector3[] verts)
    {
        int index = 0;
        float minDist = float.MaxValue;
        for (int i = 0; i < verts.Length; i++)
        {
            float d = (verts[i] - p).sqrMagnitude;
            if (d < minDist)
            {
                minDist = d;
                index = i;
            }
        }
        return index;
    }

    // Metrics
    public static float ComputeChamferDistance(List<Vector3> A, List<Vector3> B)
    {
        float sumAB = 0f, sumBA = 0f;

        foreach (var a in A)
        {
            float min = float.MaxValue;
            foreach (var b in B)
            {
                float d = Vector3.SqrMagnitude(a - b);
                if (d < min) min = d;
            }
            sumAB += min;
        }

        foreach (var b in B)
        {
            float min = float.MaxValue;
            foreach (var a in A)
            {
                float d = Vector3.SqrMagnitude(b - a);
                if (d < min) min = d;
            }
            sumBA += min;
        }

        return Mathf.Sqrt((sumAB / A.Count + sumBA / B.Count) * 0.5f);
    }

    public static float ComputeHausdorffDistance(List<Vector3> A, List<Vector3> B)
    {
        float maxAB = 0f, maxBA = 0f;

        foreach (var a in A)
        {
            float min = float.MaxValue;
            foreach (var b in B)
            {
                float d = Vector3.Distance(a, b);
                if (d < min) min = d;
            }
            if (min > maxAB) maxAB = min;
        }

        foreach (var b in B)
        {
            float min = float.MaxValue;
            foreach (var a in A)
            {
                float d = Vector3.Distance(b, a);
                if (d < min) min = d;
            }
            if (min > maxBA) maxBA = min;
        }

        return Mathf.Max(maxAB, maxBA);
    }

    public static float ComputeNormalSimilarity(List<Vector3> A, List<Vector3> B, List<Vector3> normalsA, List<Vector3> normalsB)
    {
        int count = Mathf.Min(A.Count, B.Count);
        float total = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 nA = normalsA[i].normalized;
            Vector3 nB = normalsB[i].normalized;
            float dot = Mathf.Clamp(Vector3.Dot(nA, nB), -1f, 1f);
            total += Mathf.Acos(dot) * Mathf.Rad2Deg; // angle in degrees
        }

        return total / count; // average angular deviation
    }

    //Used for comparison
    public static Mesh ScaleMeshToFitDistance(Mesh sourceMesh, float maxDistance = 1f, bool useCentroid = true)
    {
        if (sourceMesh == null || sourceMesh.vertexCount == 0)
        {
            Debug.LogWarning("ScaleMeshToFitDistance: Invalid or empty mesh.");
            return sourceMesh;
        }

        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] scaledVertices = new Vector3[vertices.Length];

        // Step 1: Compute reference center
        Vector3 center = Vector3.zero;
        if (useCentroid)
        {
            foreach (var v in vertices)
                center += v;
            center /= vertices.Length;
        }

        // Step 2: Find current maximum distance from center
        float maxFound = 0f;
        foreach (var v in vertices)
        {
            float dist = (v - center).magnitude;
            if (dist > maxFound)
                maxFound = dist;
        }

        // Avoid division by zero
        if (maxFound < 1e-8f)
        {
            Debug.LogWarning("ScaleMeshToFitDistance: Mesh is degenerate (all vertices coincident).");
            return sourceMesh;
        }

        // Step 3: Compute scale factor
        float scaleFactor = maxDistance / maxFound;

        // Step 4: Apply scaling
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 centered = vertices[i] - center;
            scaledVertices[i] = center + centered * scaleFactor;
        }

        // Step 5: Create new mesh
        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.vertices = scaledVertices;
        newMesh.RecalculateBounds();
        newMesh.RecalculateNormals();

        return newMesh;
    }


    //Compute Avg Similarity based on all distances with different weights, gamma < 1: emphasize small errors / >1 to suppress small errors
    public static float ComputeMetricsDistanceAverage(float Chamfer, float Hausdorff, float Normals, 
        float CWeight = 0.4f, float HWeight = 0.3f, float NWeight = 0.3f, 
        float MaxDistance = 1f, float gamma = 1.0f)
    {

        // If weights don't sum to >0, fallback to defaults
        float weightSum = CWeight + HWeight + NWeight;
        if (weightSum <= 0f)
        {
            CWeight = 0.5f; HWeight = 0.3f; NWeight = 0.2f;
            weightSum = 1f;
        }
        // Normalize weights so they sum to 1
        CWeight /= weightSum; HWeight /= weightSum; NWeight /= weightSum;

        //float gamma = Mathf.Max(0.0001f, opts.gamma == 0f ? 1f : opts.gamma);

        // Per-metric similarity (0..1)
        float simChamfer = Mathf.Clamp01(1f - (Chamfer / MaxDistance));
        float simHausdorff = Mathf.Clamp01(1f - (Hausdorff / MaxDistance));
        float simNormal = Mathf.Clamp01(1f - (Normals / 180f));

        // Optional gamma adjustment (non-linear sensitivity)
        // Using pow keeps values in 0..1; smaller gamma (<1) boosts small similarities
        simChamfer = Mathf.Pow(simChamfer, gamma);
        simHausdorff = Mathf.Pow(simHausdorff, gamma);
        simNormal = Mathf.Pow(simNormal, gamma);

        // Weighted combination
        float combined = simChamfer * CWeight + simHausdorff * HWeight + simNormal * NWeight;

        // Map to percentage
        return Mathf.Clamp01(combined) * 100f;
    }


    /// <summary>
    /// Calculates the Inlier Ratio: The percentage of deformed points that are within a specific 
    /// distance (tolerance) of the original shape.
    /// </summary>
    public static float ComputeInlierRatio(List<Vector3> sourceSamples, List<Vector3> targetSamples, float tolerance)
    {
        if (sourceSamples == null || targetSamples == null || sourceSamples.Count == 0) return 0f;

        int inlierCount = 0;

        foreach (Vector3 sourcePt in sourceSamples)
        {
            float minDistance = float.MaxValue;

            // Find the closest point in the target samples
            foreach (Vector3 targetPt in targetSamples)
            {
                float dist = Vector3.Distance(sourcePt, targetPt);
                if (dist < minDistance)
                {
                    minDistance = dist;
                }
            }

            // If the closest point is within tolerance, it's an inlier
            if (minDistance <= tolerance)
            {
                inlierCount++;
            }
        }

        return ((float)inlierCount / sourceSamples.Count) * 100f;
    }

    /// <summary>
    /// Approximates Volume DSC by temporarily generating colliders and sampling a 3D voxel grid.
    /// </summary>
    public static float ComputeVolumeDSC(Mesh originalMesh, Mesh deformedMesh, int resolution = 20)
    {
        // 1. Create temporary GameObjects with MeshColliders
        GameObject objA = new GameObject("TempOriginalCollider");
        GameObject objB = new GameObject("TempDeformedCollider");

        MeshCollider colA = objA.AddComponent<MeshCollider>();
        MeshCollider colB = objB.AddComponent<MeshCollider>();

        colA.sharedMesh = originalMesh;
        colB.sharedMesh = deformedMesh;

        // 2. Find the combined bounding box
        Bounds bounds = colA.bounds;
        bounds.Encapsulate(colB.bounds);

        float stepX = bounds.size.x / resolution;
        float stepY = bounds.size.y / resolution;
        float stepZ = bounds.size.z / resolution;

        int volumeA = 0, volumeB = 0, intersection = 0;

        // 3. Grid Sampling
        for (float x = bounds.min.x; x <= bounds.max.x; x += stepX)
        {
            for (float y = bounds.min.y; y <= bounds.max.y; y += stepY)
            {
                for (float z = bounds.min.z; z <= bounds.max.z; z += stepZ)
                {
                    Vector3 pt = new Vector3(x, y, z);

                    bool inA = IsPointInside(pt, colA);
                    bool inB = IsPointInside(pt, colB);

                    if (inA) volumeA++;
                    if (inB) volumeB++;
                    if (inA && inB) intersection++;
                }
            }
        }

        // 4. Clean up temporary objects
        GameObject.Destroy(objA);
        GameObject.Destroy(objB);

        if (volumeA + volumeB == 0) return 0f;

        return (2f * intersection) / (volumeA + volumeB);
    }

    // Helper for DSC
    private static bool IsPointInside(Vector3 point, MeshCollider collider)
    {
        // Fast bounds check first
        if (!collider.bounds.Contains(point)) return false;

        Ray ray = new Ray(point, Vector3.up);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        int hitCount = 0;
        foreach (var hit in hits)
        {
            if (hit.collider == collider) hitCount++;
        }

        // Odd hits = inside the mesh (requires "Queries Hit Backfaces" enabled in Unity settings)
        return hitCount % 2 != 0;
    }

    /// <summary>
    /// Converts a mesh to World Space exactly as it appears in the scene, with NO offsets.
    /// </summary>
    public static Mesh GetWorldSpaceMesh(Mesh mesh, Transform meshTransform)
    {
        Mesh copy = Object.Instantiate(mesh);
        Vector3[] verts = copy.vertices;

        // Strictly apply the object's actual transform to get true World Space coordinates
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = meshTransform.TransformPoint(verts[i]);
        }

        copy.vertices = verts;
        copy.RecalculateBounds();
        copy.RecalculateNormals();

        return copy;
    }

    /// <summary>
    /// Calculates the Surface Dice Similarity Coefficient (Boundary F1-Score).
    /// Measures the agreement between two surfaces, ignoring internal volume.
    /// </summary>
    public static float ComputeSurfaceDSC(List<Vector3> samplesA, List<Vector3> samplesB, float tolerance)
    {
        if (samplesA == null || samplesB == null || samplesA.Count == 0 || samplesB.Count == 0) return 0f;

        int overlappingPointsA = CountInliers(samplesA, samplesB, tolerance);
        int overlappingPointsB = CountInliers(samplesB, samplesA, tolerance);

        return (float)(overlappingPointsA + overlappingPointsB) / (samplesA.Count + samplesB.Count);
    }

    // Helper method
    private static int CountInliers(List<Vector3> source, List<Vector3> target, float tolerance)
    {
        int count = 0;
        foreach (Vector3 p1 in source)
        {
            float minDist = float.MaxValue;
            foreach (Vector3 p2 in target)
            {
                float dist = Vector3.Distance(p1, p2);
                if (dist < minDist) minDist = dist;
            }
            if (minDist <= tolerance) count++;
        }
        return count;
    }
}

