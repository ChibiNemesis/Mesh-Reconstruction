using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Used to Visualize slices of cut bounding boxes
[RequireComponent(typeof(BoundsSlicer))]
public class BoundsSliceVisualizer : MonoBehaviour
{
    [SerializeField]
    BoundsSlicer Slicer;

    private int CurrIter = 0;
    private int currPart = 0;
    private List<Color> Colors = new List<Color> { Color.yellow, Color.red };

    public void DrawBoundingSlicesOld()
    {
        Slicer.CreateSeperateBoxes();
        var slices = Slicer.GetSlices();

        var Parts = Slicer.PartData;

        foreach(BoundsPoints bp in slices)
        {
            DrawBoxPart(bp.Min, bp.Max);

            if (Parts[currPart] == CurrIter-1)
            {
                currPart++;
                CurrIter = 0;
            }
            else
            {
                CurrIter++;
            }
        }
    }

    public void DrawBoundingSlices()
    {
        Slicer.CreateSeperateBoxes();
        var slices = Slicer.GetSlices();
        var Parts = Slicer.PartData;

        // FIX 1: Define these INSIDE the method so they reset every frame
        int currentSliceCount = 0;
        int currentPartIndex = 0;

        foreach (BoundsPoints bp in slices)
        {
            // Safety Check: If we have more slices than defined parts, stop or use a default
            if (currentPartIndex >= Parts.Count)
            {
                Gizmos.color = Color.white; // Fallback color
            }
            else
            {
                // Set color based on current part
                Gizmos.color = Colors[currentPartIndex % Colors.Count];
            }

            DrawBoxPart(bp.Min, bp.Max);

            // FIX 2: Simplified switching logic
            if (currentPartIndex < Parts.Count)
            {
                currentSliceCount++;

                // If we have drawn enough slices for this part...
                if (currentSliceCount >= Parts[currentPartIndex])
                {
                    currentPartIndex++;     // Move to next part
                    currentSliceCount = 0;  // Reset slice counter for the new part
                }
            }
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

        var min_x = (min.x * xs) + x;
        var max_x = (max.x * xs) + x;

        var min_y = (min.y * ys) + y;
        var max_y = (max.y * ys) + y;

        var min_z = (min.z * zs) + z;
        var max_z = (max.z * zs) + z;


        Vector3 vec1 = new Vector3(min_x, max_y, min_z);
        Vector3 vec2 = new Vector3(max_x, max_y, min_z);
        Vector3 vec3 = new Vector3(min_x, min_y, min_z);
        Vector3 vec4 = new Vector3(max_x, min_y, min_z);

        Vector3 vec5 = new Vector3(min_x, max_y, max_z);
        Vector3 vec6 = new Vector3(max_x, max_y, max_z);
        Vector3 vec7 = new Vector3(min_x, min_y, max_z);
        Vector3 vec8 = new Vector3(max_x, min_y, max_z);

        //Gizmos.color = Colors[currPart % Colors.Count];

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
