using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides functionality to slice a mesh's bounds into multiple sections along a specified axis.
/// </summary>
/// <remarks>Requires a MeshFilter component. Slices are generated based on the configured axis and repeat count,
/// and stored as BoundsPoints.</remarks>
[RequireComponent(typeof(MeshFilter))]
public class BoundsSlicer : MonoBehaviour
{
    private const int MAX_SLICES = 100;

    [SerializeField]
    [Range(0, MAX_SLICES)]
    int Repeats = 0;

    [SerializeField]
    AxisCut axis = AxisCut.X;

    [SerializeField]
    MeshFilter mesh;

    [SerializeField]
    public List<int> PartData;

    public List<BoundsPoints> Slices;

    private void Start()
    {
        if(mesh != null)
            mesh = GetComponent<MeshFilter>();
    }

    /// <summary>
    /// Generates separate bounding boxes based on the mesh's bounds and the specified repeat count.
    /// </summary>
    public void CreateSeperateBoxes()
    {
        Bounds bounds = mesh.sharedMesh.bounds;
        CreateSeperateBoxesInner(Repeats - 1, bounds.min, bounds.max);
    }

    /// <summary>
    /// Recursively divides a bounding box into separate slices along a specified axis and stores the resulting bounds.
    /// </summary>
    /// <param name="reps">The number of recursive divisions to perform.</param>
    /// <param name="min">The minimum coordinates of the bounding box.</param>
    /// <param name="max">The maximum coordinates of the bounding box.</param>
    private void CreateSeperateBoxesInner(int reps, Vector3 min, Vector3 max)
    {
        //create list on first iteration
        if (reps == Repeats - 1)
        {
            Slices = new List<BoundsPoints>();
        }

        if (reps == 0)
        {
            Slices.Add(new BoundsPoints(min, max));
            return;
        }

        Vector3 minRec = new Vector3();
        Vector3 maxRec = new Vector3();

        minRec.x = min.x;
        minRec.y = min.y;
        minRec.z = min.z;

        maxRec.x = max.x;
        maxRec.y = max.y;
        maxRec.z = max.z;

        if (axis == AxisCut.X)
        {
            float dist_min_max = Mathf.Abs(max.x - min.x);
            maxRec.x = max.x - (dist_min_max / (reps + 1));
            minRec.x = maxRec.x;
        }
        else if (axis == AxisCut.Y)
        {
            float dist_min_max = Mathf.Abs(max.y - min.y);
            maxRec.y = max.y - (dist_min_max / (reps + 1));
            minRec.y = maxRec.y;
        }
        else
        {
            float dist_min_max = Mathf.Abs(max.z - min.z);
            maxRec.z = max.z - (dist_min_max / (reps + 1));
            minRec.z = maxRec.z;
        }

        Slices.Add(new BoundsPoints(minRec, max));

        CreateSeperateBoxesInner(reps - 1, min, maxRec);
    }

    // Returns the list of bounding boxes generated from slicing the mesh's bounds.
    public List<BoundsPoints> GetSlices() { return Slices; }

    // Returns the axis along which the bounds are sliced.
    public AxisCut GetAxis() { return axis; }
}
