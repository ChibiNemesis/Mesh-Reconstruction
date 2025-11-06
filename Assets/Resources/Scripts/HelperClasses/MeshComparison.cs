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
        Vector3 min = mesh.bounds.min;
        Vector3 max = mesh.bounds.max;
        Vector3 center = (min + max) * 0.5f;
        float scale = 1f / (max - min).magnitude;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 world = meshTransform.TransformPoint(verts[i]);
            verts[i] = (world - center) * scale;
        }

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
}

