using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Used to Visualize slices of cut bounding boxes
//Probably just for debugging in editor
[RequireComponent(typeof(BoundsSlicer))]
public class BoundsSliceVisualizer : MonoBehaviour
{
    [SerializeField]
    BoundsSlicer Slicer;

    public void DrawBoundingSlices()
    {
        Slicer.CreateSeperateBoxes();
        var slices = Slicer.GetSlices();

        foreach(BoundsPoints bp in slices)
        {
            DrawBoxPart(bp.Min, bp.Max);
        }
    }

    private void DrawBoxPart(Vector3 min, Vector3 max)
    {
        float x, y, z, xs, ys, zs;
        //Use this to transform all slices to new area
        x = transform.position.x;
        y = transform.position.y;
        z = transform.position.z;

        //Also, use this for scaling in case scale is not 1 (WIP)
        xs = transform.localScale.x;
        ys = transform.localScale.y;
        zs = transform.localScale.z;

        Vector3 vec1 = new Vector3((min.x * xs + x), ((max.y*ys) + y), ((min.z * zs) + z));
        Vector3 vec2 = new Vector3((max.x * xs + x), ((max.y*ys) + y), ((min.z * zs) + z));
        Vector3 vec3 = new Vector3((min.x*xs + x), ((min.y*ys) + y), ((min.z * zs) + z));
        Vector3 vec4 = new Vector3((max.x*xs + x), ((min.y*ys) + y), ((min.z * zs) + z));

        Vector3 vec5 = new Vector3((min.x*xs + x), ((max.y*ys) + y), ((max.z * zs) + z));
        Vector3 vec6 = new Vector3((max.x*xs + x), ((max.y*ys) + y), ((max.z * zs) + z));
        Vector3 vec7 = new Vector3((min.x*xs + x), ((min.y*ys) + y), ((max.z * zs) + z));
        Vector3 vec8 = new Vector3((max.x*xs + x), ((min.y*ys) + y), ((max.z * zs) + z));

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(vec1, vec2);
        Gizmos.DrawLine(vec1, vec3);
        Gizmos.DrawLine(vec1, vec5);
        Gizmos.DrawLine(vec3, vec4);
        Gizmos.DrawLine(vec3, vec7);
        Gizmos.DrawLine(vec5, vec6);
        Gizmos.DrawLine(vec5, vec7);
        Gizmos.DrawLine(vec7, vec8);
        Gizmos.DrawLine(vec4, vec8);
        Gizmos.DrawLine(vec6, vec8);
        Gizmos.DrawLine(vec2, vec6);
        Gizmos.DrawLine(vec2, vec4);
    }

    private void OnDrawGizmosSelected()
    {
        DrawBoundingSlices();
    }
}
