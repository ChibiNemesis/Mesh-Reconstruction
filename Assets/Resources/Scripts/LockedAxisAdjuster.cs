using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockedAxisAdjuster : MonoBehaviour
{
    [SerializeField]
    SliceReshaper shaper;

    [SerializeField]
    BoundsSlicer slicer;

    //Scale multiplier for each part
    [SerializeField]
    List<float> PartScale;

    void Start()
    {
        if (shaper == null)
        {
            shaper = gameObject.GetComponent<SliceReshaper>();
        }

        if (slicer == null)
        {
            slicer = gameObject.GetComponent<BoundsSlicer>();
        }
    }

    public void AdjustLockedAxis()
    {
        // 1. Dependency Checks
        if (shaper == null || slicer == null)
        {
            Debug.LogError("LockedAxisAdjuster: Missing dependencies (SliceReshaper or BoundsSlicer).");
            return;
        }

        if (slicer.PartData == null || PartScale == null || slicer.PartData.Count != PartScale.Count)
        {
            Debug.LogWarning($"LockedAxisAdjuster: PartScale count ({PartScale?.Count}) must match Slicer PartData count ({slicer.PartData?.Count}).");
            return;
        }

        AxisCut axis = slicer.GetAxis();

        // 2. Initialize Anchor
        // We find the absolute bottom of the first part to anchor the whole model.
        // This keeps the model in its original world position.
        int globalSliceIndex = 0;
        // FindTop = false gets the minimum value (bottom)
        float currentGlobalBase = FindEdgePoint(0, slicer.PartData[0], axis, FindTop: false);

        // 3. Iterate through each Part
        for (int i = 0; i < slicer.PartData.Count; i++)
        {
            var partData = slicer.PartData[i];
            int partSliceCount = partData;
            float scaleFactor = PartScale[i];

            int startSlice = globalSliceIndex;
            int endSlice = globalSliceIndex + partSliceCount;

            // A. Find the local boundaries of this part (Before Scaling)
            float partBottomVertex = FindEdgePoint(startSlice, endSlice, axis, FindTop: false);
            float partTopVertex = FindEdgePoint(startSlice, endSlice, axis, FindTop: true);

            // Calculate actual height of this segment
            float originalHeight = Mathf.Abs(partTopVertex - partBottomVertex);

            // B. Scale all slices within this part
            for (int s = startSlice; s < endSlice; s++)
            {
                if (s >= shaper.SliceGrabbers.Count) break;
                var slice = shaper.SliceGrabbers[s];

                // Apply Scaling: 
                // Pos = GlobalBase + (LocalPos - PartBottom) * ScaleFactor
                ApplyScalingToVectorList(slice.Destinations, axis, partBottomVertex, currentGlobalBase, scaleFactor);

                if (slice.OuterDestinations != null)
                    ApplyScalingToVectorList(slice.OuterDestinations, axis, partBottomVertex, currentGlobalBase, scaleFactor);

                if (slice.InnerDestinations != null)
                    ApplyScalingToVectorList(slice.InnerDestinations, axis, partBottomVertex, currentGlobalBase, scaleFactor);
            }

            // C. Update the Base for the NEXT part
            // The next part starts exactly where this scaled part ends
            currentGlobalBase += (originalHeight * scaleFactor);

            // Advance index
            globalSliceIndex += partSliceCount;
        }
    }

    private float FindEdgePoint(int FirstIndex, int LastIndex, AxisCut axis, bool FindTop = true)
    {
        var sg = shaper.SliceGrabbers;
        float Edge;
        if (FindTop)
        {
            Edge = -1000f;

            for (int index = FirstIndex; index < LastIndex; index++)
            {
                var sliceDestinations = sg[index].Destinations;

                foreach (var final in sliceDestinations)
                {
                    if (axis == AxisCut.X)
                    {
                        if (final.x > Edge)
                        {
                            Edge = final.x;
                        }
                    }
                    if (axis == AxisCut.Y)
                    {
                        if (final.y > Edge)
                        {
                            Edge = final.y;
                        }
                    }
                    if (axis == AxisCut.Z)
                    {
                        if (final.z > Edge)
                        {
                            Edge = final.z;
                        }
                    }
                }
            }
        }
        else
        {
            Edge = 1000f;

            for (int index = FirstIndex; index < LastIndex; index++)
            {
                var sliceDestinations = sg[index].Destinations;

                foreach (var final in sliceDestinations)
                {
                    if (axis == AxisCut.X)
                    {
                        if (final.x < Edge)
                        {
                            Edge = final.x;
                        }
                    }
                    if (axis == AxisCut.Y)
                    {
                        if (final.y < Edge)
                        {
                            Edge = final.y;
                        }
                    }
                    if (axis == AxisCut.Z)
                    {
                        if (final.z < Edge)
                        {
                            Edge = final.z;
                        }
                    }
                }
            }
        }

        return Edge;
    }

    // Applies the math: NewPos = GlobalBase + (OldPos - PartBottom) * Scale
    private void ApplyScalingToVectorList(List<Vector3> list, AxisCut axis, float partBottom, float globalBase, float scale)
    {
        for (int k = 0; k < list.Count; k++)
        {
            Vector3 pos = list[k];
            float currentVal = GetAxisValue(pos, axis);

            // Normalize to local part space (0..Length)
            float relativeVal = currentVal - partBottom;

            // Scale and offset to global space
            float newVal = globalBase + (relativeVal * scale);

            list[k] = SetAxisValue(pos, axis, newVal);
        }
    }

    private float GetAxisValue(Vector3 v, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.X => v.x,
            AxisCut.Y => v.y,
            AxisCut.Z => v.z,
            _ => v.y
        };
    }

    private Vector3 SetAxisValue(Vector3 v, AxisCut axis, float val)
    {
        switch (axis)
        {
            case AxisCut.X: v.x = val; break;
            case AxisCut.Y: v.y = val; break;
            case AxisCut.Z: v.z = val; break;
        }
        return v;
    }
}
