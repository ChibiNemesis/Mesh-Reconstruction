using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizes bounding box slices in the Unity Editor using Gizmos, based on data from a BoundsSlicer component.
/// </summary>
/// <remarks>Requires a BoundsSlicer component on the same GameObject. Colors and slice grouping are configurable
/// for visual distinction of parts.</remarks>
//[RequireComponent(typeof(BoundsSlicer))]
public class BoundsSliceVisualizer : MonoBehaviour
{
    [SerializeField]
    public BoundsSlicer Slicer;

    private List<Color> Colors = new List<Color> { Color.yellow, Color.red };

    private void Reset()
    {
        if (Slicer == null)
        {
            Slicer = GetComponent<BoundsSlicer>();
        }
    }

    // Draws the bounding slices as defined by the BoundsSlicer component, applying color coding based on part grouping.
    public void DrawBoundingSlices()
    {
        // SAFETY CHECK 1: Ensure Slicer exists
        if (Slicer == null) return;

        var Parts = Slicer.PartData;

        // SAFETY CHECK 2: Don't do anything if PartData isn't set up
        if (Parts == null || Parts.Count == 0) return;

        // Calling CreateSeperateBoxes() here is what freezes your Editor if it contains heavy math.
        // It is highly recommended to remove this line, and instead call CreateSeperateBoxes() 
        // from inside a private void OnValidate() method inside your BoundsSlicer.cs script!
        Slicer.CreateSeperateBoxes();

        var slices = Slicer.GetSlices();

        // SAFETY CHECK 3: Ensure slices actually generated successfully
        if (slices == null || slices.Count == 0) return;

        int currentSliceCount = 0;
        int currentPartIndex = 0;

        foreach (var bp in slices)
        {
            if (bp == null) continue; // Safety check for null array elements

            // Safety Check: If we have more slices than defined parts, use a fallback
            if (currentPartIndex >= Parts.Count)
            {
                Gizmos.color = Color.white;
            }
            else
            {
                Gizmos.color = Colors[currentPartIndex % Colors.Count];
            }

            DrawBoxPart(bp.Min, bp.Max);

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

    // Draws a box defined by min and max points, applying the GameObject's position and scale.
    private void DrawBoxPart(Vector3 min, Vector3 max)
    {
        float x = transform.position.x;
        float y = transform.position.y;
        float z = transform.position.z;

        float xs = transform.localScale.x;
        float ys = transform.localScale.y;
        float zs = transform.localScale.z;

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
