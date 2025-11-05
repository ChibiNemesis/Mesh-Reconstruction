using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MeshComparison
{

    // Normalize
    private static Mesh NormalizeMesh(Mesh mesh, Transform meshTransform)
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
}

