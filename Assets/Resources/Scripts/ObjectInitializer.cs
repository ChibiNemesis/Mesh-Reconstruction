using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectInitializer : SliceInitializer
{
    [SerializeField]
    SliceReshaper shaper;

    [SerializeField]
    GameObject ObjectToRecreate;

    public override void InitializeSlices()
    {
        if (shaper == null || ObjectToRecreate == null)
        {
            Debug.LogWarning("Missing reference to SliceReshaper or ObjectToRecreate.");
            return;
        }

        MeshFilter meshFilter = ObjectToRecreate.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogWarning($"Mesh Filter component not found inside {ObjectToRecreate.name}");
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            Debug.LogWarning("MeshFilter has no mesh assigned.");
            return;
        }

        // Collect unique vertices in world space (avoid duplicates)
        List<Vector3> uniqueVertices = new List<Vector3>();
        List<Vector3> uniqueVerticesV2 = new List<Vector3>(); //Same vertices, but transform them based on this object's Transform instead

        Transform meshTransform = ObjectToRecreate.transform;
        foreach (var v in mesh.vertices)
        {
            Vector3 worldV = meshTransform.TransformPoint(v);
            Vector3 worldV2 = this.transform.TransformPoint(v);
            if (!uniqueVertices.Contains(worldV))
            {
                uniqueVertices.Add(worldV);
                uniqueVerticesV2.Add(worldV2);
            }
        }

        var ObjectSlicer = ObjectToRecreate.GetComponent<BoundsSlicer>();
        ObjectSlicer.CreateSeperateBoxes();

        float x, y, z, xs, ys, zs;
        var ADDITION = 0.0001f;
        //Use this to transform all slices to new area
        x = ObjectToRecreate.transform.position.x;
        y = ObjectToRecreate.transform.position.y;
        z = ObjectToRecreate.transform.position.z;

        //Also, use this for scaling in case scale is not 1
        xs = ObjectToRecreate.transform.localScale.x;
        ys = ObjectToRecreate.transform.localScale.y;
        zs = ObjectToRecreate.transform.localScale.z;

        // For each slice in the shaper, build its destination list via Hungarian mapping
        for (int s = 0; s < shaper.SliceGrabbers.Count; s++)
        {
            SliceData slice = shaper.SliceGrabbers[s];
            slice.Destinations = new List<Vector3>();

            // Gather all vertices inside this slice's bounds
            List<Vector3> verticesInSlice = new List<Vector3>();

            var ObjectSlice = ObjectSlicer.GetSlices()[s]; //Surely refine code at some point
            Bounds sliceBounds = new Bounds();

            var Nmin = new Vector3((ObjectSlice.Min.x * xs) + x - ADDITION, (ObjectSlice.Min.y * ys) + y - ADDITION, (ObjectSlice.Min.z * zs) + z - ADDITION);
            var Nmax = new Vector3((ObjectSlice.Max.x * xs) + x + ADDITION, (ObjectSlice.Max.y * ys) + y + ADDITION, (ObjectSlice.Max.z * zs) + z + ADDITION);
            sliceBounds.SetMinMax(Nmin, Nmax);

            foreach (var v in uniqueVertices)
            {
                if (sliceBounds.Contains(v))
                    verticesInSlice.Add(v);
            }
            for(int v=0; v<uniqueVertices.Count; v++)
            {
                if (sliceBounds.Contains(uniqueVertices[v]))
                {
                    verticesInSlice.Add(uniqueVerticesV2[v]);
                }
            }

            List<GameObject> grabbers = slice.Grabbers;
            if (grabbers.Count == 0 || verticesInSlice.Count == 0)
            {
                Debug.LogWarning($"Slice {s}: missing grabbers or vertices in slice.");
                continue;
            }

            // Build cost matrix (distance between each grabber and each vertex)
            float[,] costMatrix = new float[grabbers.Count, verticesInSlice.Count];
            for (int i = 0; i < grabbers.Count; i++)
            {
                Vector3 gpos = grabbers[i].transform.position;
                for (int j = 0; j < verticesInSlice.Count; j++)
                {
                    costMatrix[i, j] = Vector3.Distance(gpos, verticesInSlice[j]);
                }
            }

            // Run Hungarian algorithm to find optimal assignment
            int[] assignment = HungarianAlgorithm.Solve(costMatrix);

            // Store destination positions
            for (int i = 0; i < grabbers.Count; i++)
            {
                int assignedIndex = assignment[i];
                if (assignedIndex >= 0 && assignedIndex < verticesInSlice.Count)
                    slice.Destinations.Add(verticesInSlice[assignedIndex]);
                else
                    slice.Destinations.Add(grabbers[i].transform.position); // fallback
            }
        }

        Debug.Log("ObjectInitializer: Grabber destinations initialized using Hungarian assignment.");
    }
}
