using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MultiplePass : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    int SamplesPerPass = 20;

    // 1 = Y only, 2 = Y+X, 3 = Y+X+Z
    [Range(1, 3)]
    [SerializeField]
    int AxisNum = 1;

    [SerializeField]
    GameObject ContourAsset;

    // Data Stores
    public List<List<Vector3>> YRings = new List<List<Vector3>>();
    public List<List<Vector3>> XRings = new List<List<Vector3>>();
    public List<List<Vector3>> ZRings = new List<List<Vector3>>();

    private const int X_AXIS = 1;
    private const int Y_AXIS = 2;
    private const int Z_AXIS = 3;

    void Start()
    {
        if (ContourAsset == null)
        {
            Debug.LogError("ContourAsset is missing!");
            return;
        }

        // 1. Perform Sampling (Result is in Original World Space)
        SampleYAxis();

        if (AxisNum >= 2) SampleXAxis();
        if (AxisNum >= 3) SampleZAxis();

        // 2. Shift all points so the object center aligns with THIS transform
        RecenterPoints();
    }

    // --- RE-CENTERING LOGIC ---
    private void RecenterPoints()
    {
        // Collect all points to find the current bounds center
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool hasPoints = false;

        void CheckBounds(List<List<Vector3>> rings)
        {
            foreach (var ring in rings)
            {
                foreach (var p in ring)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    hasPoints = true;
                }
            }
        }

        CheckBounds(YRings);
        CheckBounds(XRings);
        CheckBounds(ZRings);

        if (!hasPoints) return;

        // Calculate the center of the sampled data
        Vector3 currentCenter = (min + max) * 0.5f;

        // Calculate offset needed to move that center to this GameObject's position
        Vector3 offset = transform.position - currentCenter;

        // Apply offset to all lists
        ApplyOffset(YRings, offset);
        ApplyOffset(XRings, offset);
        ApplyOffset(ZRings, offset);
    }

    private void ApplyOffset(List<List<Vector3>> rings, Vector3 offset)
    {
        for (int i = 0; i < rings.Count; i++)
        {
            for (int j = 0; j < rings[i].Count; j++)
            {
                rings[i][j] += offset;
            }
        }
    }

    // --- PASS 1: Y-AXIS ---
    private void SampleYAxis()
    {
        YRings.Clear();
        for (int c = 0; c < ContourAsset.transform.childCount; c++)
        {
            var child = ContourAsset.transform.GetChild(c);
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            Vector3[] verts = mf.sharedMesh.vertices;
            List<Vector3> worldVerts = new List<Vector3>();
            for (int i = 0; i < verts.Length; i++)
                worldVerts.Add(child.transform.TransformPoint(verts[i]));

            List<Vector3> sampledRing = SamplePerimeter(worldVerts, SamplesPerPass);
            Vector3 center = GetCentroid(sampledRing);
            sampledRing = sampledRing.OrderBy(v => Mathf.Atan2(v.z - center.z, v.x - center.x)).ToList();

            YRings.Add(sampledRing);
        }
    }

    // --- PASS 2: X-AXIS ---
    private void SampleXAxis()
    {
        XRings.Clear();
        List<GameObject> slices = GetSlices();
        float minX = FindMinMaxValue(slices, X_AXIS, true);
        float maxX = FindMinMaxValue(slices, X_AXIS, false);
        float step = (maxX - minX) / SamplesPerPass;

        for (int i = 0; i < SamplesPerPass; i++)
        {
            float currentPlaneX = minX + (step * i) + (step * 0.5f);
            List<Vector3> virtualRing = new List<Vector3>();

            foreach (var slice in slices)
            {
                var intersects = IntersectContourWithPlane(slice, X_AXIS, currentPlaneX);
                virtualRing.AddRange(intersects);
            }

            if (virtualRing.Count > 2)
            {
                Vector3 center = GetCentroid(virtualRing);
                virtualRing = virtualRing.OrderBy(v => Mathf.Atan2(v.y - center.y, v.z - center.z)).ToList();
                XRings.Add(virtualRing);
            }
        }
    }

    // --- PASS 3: Z-AXIS ---
    private void SampleZAxis()
    {
        ZRings.Clear();
        List<GameObject> slices = GetSlices();
        float minZ = FindMinMaxValue(slices, Z_AXIS, true);
        float maxZ = FindMinMaxValue(slices, Z_AXIS, false);
        float step = (maxZ - minZ) / SamplesPerPass;

        for (int i = 0; i < SamplesPerPass; i++)
        {
            float currentPlaneZ = minZ + (step * i) + (step * 0.5f);
            List<Vector3> virtualRing = new List<Vector3>();

            foreach (var slice in slices)
            {
                var intersects = IntersectContourWithPlane(slice, Z_AXIS, currentPlaneZ);
                virtualRing.AddRange(intersects);
            }

            if (virtualRing.Count > 2)
            {
                Vector3 center = GetCentroid(virtualRing);
                virtualRing = virtualRing.OrderBy(v => Mathf.Atan2(v.y - center.y, v.x - center.x)).ToList();
                ZRings.Add(virtualRing);
            }
        }
    }

    // --- HELPERS ---
    private List<Vector3> IntersectContourWithPlane(GameObject slice, int cutAxis, float planeVal)
    {
        List<Vector3> hits = new List<Vector3>();
        Mesh m = slice.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] v = m.vertices;
        int count = v.Length;
        for (int i = 0; i < count; i++)
        {
            Vector3 p1 = slice.transform.TransformPoint(v[i]);
            Vector3 p2 = slice.transform.TransformPoint(v[(i + 1) % count]);

            float val1 = (cutAxis == X_AXIS) ? p1.x : p1.z;
            float val2 = (cutAxis == X_AXIS) ? p2.x : p2.z;

            if ((val1 < planeVal && val2 >= planeVal) || (val1 >= planeVal && val2 < planeVal))
            {
                float t = (planeVal - val1) / (val2 - val1);
                hits.Add(Vector3.Lerp(p1, p2, t));
            }
        }
        return hits;
    }

    private List<GameObject> GetSlices()
    {
        List<GameObject> list = new List<GameObject>();
        for (int i = 0; i < ContourAsset.transform.childCount; i++)
            list.Add(ContourAsset.transform.GetChild(i).gameObject);
        return list;
    }

    private float FindMinMaxValue(List<GameObject> slices, int axis, bool FindMin = true)
    {
        float val = FindMin ? float.MaxValue : float.MinValue;
        foreach (var slice in slices)
        {
            var mf = slice.GetComponent<MeshFilter>();
            if (mf == null) continue;
            var ver = mf.sharedMesh.vertices;
            for (int v = 0; v < ver.Length; v++)
            {
                Vector3 worldPt = slice.transform.TransformPoint(ver[v]);
                float comp = (axis == X_AXIS) ? worldPt.x : worldPt.z;
                if (FindMin) { if (comp < val) val = comp; }
                else { if (comp > val) val = comp; }
            }
        }
        return val;
    }

    private List<Vector3> SamplePerimeter(List<Vector3> points, int count)
    {
        if (points.Count <= count) return new List<Vector3>(points);
        List<Vector3> result = new List<Vector3>();
        float step = (float)points.Count / count;
        for (int i = 0; i < count; i++)
        {
            int idx = Mathf.FloorToInt(i * step);
            result.Add(points[idx]);
        }
        return result;
    }

    private Vector3 GetCentroid(List<Vector3> pts)
    {
        if (pts.Count == 0) return Vector3.zero;
        Vector3 c = Vector3.zero;
        foreach (var p in pts) c += p;
        return c / pts.Count;
    }

    // --- VISUALIZATION UPDATE ---
    void OnDrawGizmos()
    {
        DrawRingList(YRings, Color.green);
        DrawRingList(XRings, Color.red);
        DrawRingList(ZRings, Color.blue);
    }

    private void DrawRingList(List<List<Vector3>> rings, Color c)
    {
        Gizmos.color = c;
        foreach (var ring in rings)
        {
            if (ring.Count < 2) continue;

            // Draw lines connecting points
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 p1 = ring[i];
                Vector3 p2 = ring[(i + 1) % ring.Count]; // Wrap around to close loop
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
