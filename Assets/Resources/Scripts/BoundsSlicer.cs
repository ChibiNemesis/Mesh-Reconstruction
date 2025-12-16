using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Used to Create Slices on a given mesh
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

    public AxisCut GetAxis() { return axis; }

    public void CreateSeperateBoxes()
    {
        Bounds bounds = mesh.sharedMesh.bounds;
        CreateSeperateBoxesInner(Repeats, bounds.min, bounds.max);
    }

    private void CreateSeperateBoxesInner(int reps, Vector3 min, Vector3 max)
    {
        //create list on first iteration
        if (reps == Repeats)
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

    public List<BoundsPoints> GetSlices() { return Slices; }
}
