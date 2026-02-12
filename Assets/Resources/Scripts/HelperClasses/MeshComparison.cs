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

    // --- 1. NORMALIZATION WITH SCALE PRESERVATION ---

    // Centers mesh and scales it so its largest dimension is exactly 1.0
    // Returns the scale factor used.
    public static Mesh NormalizeAndUnitScale(Mesh source, Transform tr, Vector3 centerPos, out float appliedScale)
    {
        Mesh copy = Object.Instantiate(source);
        Vector3[] verts = copy.vertices;

        // Transform to World
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = tr.TransformPoint(verts[i]);
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }

        // Calculate Scale to fit Unit Cube
        float maxDim = Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z));
        appliedScale = maxDim > 1e-6f ? (1f / maxDim) : 1f;

        // Apply
        Vector3 centroid = (min + max) * 0.5f; // Use calculated bounds center
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = (verts[i] - centerPos) * appliedScale; // Shift to 0 using target center, then scale
        }

        copy.vertices = verts;
        copy.RecalculateBounds();
        copy.RecalculateNormals();
        return copy;
    }

    // Centers mesh but applies a FORCED scale factor (from the reference mesh)
    public static Mesh NormalizeWithFixedScale(Mesh source, Transform tr, Vector3 centerPos, float fixedScale)
    {
        Mesh copy = Object.Instantiate(source);
        Vector3[] verts = copy.vertices;

        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 worldPt = tr.TransformPoint(verts[i]);
            // Center then Scale
            verts[i] = (worldPt - centerPos) * fixedScale;
        }

        copy.vertices = verts;
        copy.RecalculateBounds();
        copy.RecalculateNormals();
        return copy;
    }

    // --- 2. VOLUMETRIC METRIC (DICE PROXY) ---

    // Compares the intersection of Bounding Boxes (Fast Dice Proxy)
    // Returns 0..100 percentage
    public static float ComputeBoundsVolumeSimilarity(Mesh refMesh, Mesh defMesh)
    {
        Bounds bRef = refMesh.bounds;
        Bounds bDef = defMesh.bounds;

        // Intersection Bounds
        Vector3 minI = Vector3.Max(bRef.min, bDef.min);
        Vector3 maxI = Vector3.Min(bRef.max, bDef.max);

        float intersectionVol = 0f;
        if (minI.x < maxI.x && minI.y < maxI.y && minI.z < maxI.z)
        {
            Vector3 diff = maxI - minI;
            intersectionVol = diff.x * diff.y * diff.z;
        }

        float volRef = bRef.size.x * bRef.size.y * bRef.size.z;
        float volDef = bDef.size.x * bDef.size.y * bDef.size.z;

        // Dice Formula: 2 * (Intersection) / (VolA + VolB)
        float dice = (2f * intersectionVol) / (volRef + volDef + 1e-8f);
        return Mathf.Clamp01(dice) * 100f;
    }

    // --- 3. EXPONENTIAL SCORING ---

    public static float ComputeMetricsExponential(float Chamfer, float Hausdorff, float Normals, float VolumeSim,
        float CWeight = 0.4f, float HWeight = 0.2f, float NWeight = 0.2f, float VWeight = 0.2f)
    {
        // 1. Exponential Decay for Distances
        // k = Strictness. Higher k = penalty drops faster.
        // For Unit Scale mesh: Chamfer > 0.05 is bad.
        // e^(-20 * 0.05) = e^(-1) = 0.36 (36% score). Very strict!
        // e^(-10 * 0.05) = 0.60 (60% score).
        float k = 15f;

        float scoreChamfer = Mathf.Exp(-k * Chamfer);
        float scoreHausdorff = Mathf.Exp(-(k * 0.5f) * Hausdorff); // Hausdorff is usually larger, so we relax k slightly

        // 2. Normal Similarity (Linear is usually fine, but let's square it for strictness)
        // Normals input is degrees (0..180). We want 0->1, 180->0
        float normRatio = 1f - (Normals / 180f);
        float scoreNormal = normRatio * normRatio; // Quadratic punishment for rotation errors

        // 3. Volume Similarity (Already 0-100, convert to 0-1)
        float scoreVolume = VolumeSim / 100f;

        // 4. Weighted Sum
        float total = (scoreChamfer * CWeight) +
                      (scoreHausdorff * HWeight) +
                      (scoreNormal * NWeight) +
                      (scoreVolume * VWeight);

        return Mathf.Clamp01(total) * 100f;
    }
}

