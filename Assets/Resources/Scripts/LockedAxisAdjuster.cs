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

/*
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
*/

    public void AdjustLockedAxis()
    {
        // 1. Validation
        if (shaper == null || slicer == null) return;
        if (slicer.PartData == null || PartScale == null || slicer.PartData.Count != PartScale.Count)
        {
            Debug.LogWarning("LockedAxisAdjuster: Mismatch between PartData and PartScale counts.");
            return;
        }

        AxisCut axis = slicer.GetAxis();
        int TotalParts = slicer.PartData.Count;

        // 2. Determine Strategy
        if (TotalParts == 1)
        {
            ScaleSingle(axis);
        }
        else if (TotalParts == 2)
        {
            ScaleDouble(axis);
        }
        else if (TotalParts >= 3)
        {
            ScaleMultiple(axis);
        }
    }

    // Case 1: Single Part -> Scale from CENTER (Equal expansion up and down)
    private void ScaleSingle(AxisCut axis)
    {
        int sliceStart = 0;
        int sliceEnd = slicer.PartData[0];
        float scale = PartScale[0];

        // Find Center
        float top = FindEdgePoint(sliceStart, sliceEnd, axis, true);
        float bottom = FindEdgePoint(sliceStart, sliceEnd, axis, false);
        float center = (top + bottom) * 0.5f;

        // Scale relative to Center
        ScalePartAroundPivot(sliceStart, sliceEnd, axis, center, scale);
    }

    // Case 2: Fixed Interface (Seam)
    private void ScaleDouble(AxisCut axis)
    {
        // 1. Identify the Seam (Interface)
        // This is the Top of Part 0.
        int p0_Start = 0;
        int p0_End = slicer.PartData[0];

        // Find the Seam: The geometric top of the bottom part.
        float seam = FindEdgePoint(p0_Start, p0_End, axis, FindTop: true);

        // 2. Scale Part 0 (Bottom)
        // Pivot = Seam. Scaling moves vertices DOWN (Away from Seam).
        // Vertices cannot go ABOVE the seam because (Pos - Pivot) is negative.
        float scale0 = PartScale[0];
        if (Mathf.Abs(scale0) > 0.0001f && Mathf.Abs(scale0 - 1f) > 0.0001f)
        {
            ScalePartAroundPivot(p0_Start, p0_End, axis, seam, scale0);
        }

        // 3. Scale Part 1 (Top)
        // Pivot = Seam. Scaling moves vertices UP (Away from Seam).
        int p1_Start = p0_End;
        int p1_End = p1_Start + slicer.PartData[1];

        float scale1 = PartScale[1];
        if (Mathf.Abs(scale1) > 0.0001f && Mathf.Abs(scale1 - 1f) > 0.0001f)
        {
            ScalePartAroundPivot(p1_Start, p1_End, axis, seam, scale1);
        }
    }

    // Case 3: Multiple Parts
    private void ScaleMultiple(AxisCut axis)
    {
        int count = slicer.PartData.Count;

        // 1. Handle Bottom Part (Index 0)
        // Acts exactly like ScaleDouble: Pivot is its Top Edge.
        int bStart = GetPartStartIndex(0);
        int bEnd = bStart + slicer.PartData[0];
        float bPivot = FindEdgePoint(bStart, bEnd, axis, FindTop: true);
        float bScale = PartScale[0];

        if (Mathf.Abs(bScale) > 0.0001f && Mathf.Abs(bScale - 1f) > 0.0001f)
        {
            ScalePartAroundPivot(bStart, bEnd, axis, bPivot, bScale);
        }

        // 2. Handle Top Part (Index Count-1)
        // Acts exactly like ScaleDouble: Pivot is its Bottom Edge.
        int tStart = GetPartStartIndex(count - 1);
        int tEnd = tStart + slicer.PartData[count - 1];
        float tPivot = FindEdgePoint(tStart, tEnd, axis, FindTop: false);
        float tScale = PartScale[count - 1];

        if (Mathf.Abs(tScale) > 0.0001f && Mathf.Abs(tScale - 1f) > 0.0001f)
        {
            ScalePartAroundPivot(tStart, tEnd, axis, tPivot, tScale);
        }

        // 3. Handle Middle Parts (Index 1 to Count-2)
        // As you requested: Scale Middle, then shift everything above/below.
        for (int i = 1; i < count - 1; i++)
        {
            float scale = PartScale[i];
            // Skip if 0 or 1
            if (Mathf.Abs(scale) < 0.0001f || Mathf.Abs(scale - 1f) < 0.0001f) continue;

            int mStart = GetPartStartIndex(i);
            int mEnd = mStart + slicer.PartData[i];

            float oldTop = FindEdgePoint(mStart, mEnd, axis, true);
            float oldBottom = FindEdgePoint(mStart, mEnd, axis, false);
            float center = (oldTop + oldBottom) * 0.5f;
            float oldHeight = Mathf.Abs(oldTop - oldBottom);

            // A. Scale Middle Part around CENTER
            ScalePartAroundPivot(mStart, mEnd, axis, center, scale);

            // B. Calculate Displacement
            float newHeight = oldHeight * scale;
            float totalDiff = newHeight - oldHeight;
            float shiftAmount = totalDiff * 0.5f;

            // C. Shift Bottom Group Downwards
            // (All parts from 0 to i-1)
            for (int below = 0; below < i; below++)
            {
                ShiftPart(below, axis, -shiftAmount);
            }

            // D. Shift Top Group Upwards
            // (All parts from i+1 to End)
            for (int above = i + 1; above < count; above++)
            {
                ShiftPart(above, axis, shiftAmount);
            }
        }
    }

    // --- Core Helpers ---

    // Scales a specific range of slices relative to a specific Pivot value
    // Formula: NewPos = Pivot + (OldPos - Pivot) * Scale
    /*private void ScalePartAroundPivot(int startSlice, int endSlice, AxisCut axis, float pivot, float scale)
    {
        for (int s = startSlice; s < endSlice; s++)
        {
            if (s >= shaper.SliceGrabbers.Count) break;
            var slice = shaper.SliceGrabbers[s];

            ApplyScaleLogic(slice.Destinations, axis, pivot, scale);
            if (slice.OuterDestinations != null) ApplyScaleLogic(slice.OuterDestinations, axis, pivot, scale);
            if (slice.InnerDestinations != null) ApplyScaleLogic(slice.InnerDestinations, axis, pivot, scale);
        }
    }*/

    // --- Core Math (Pivot Scaling) ---
    private void ScalePartAroundPivot(int startSlice, int endSlice, AxisCut axis, float pivot, float scale)
    {
        for (int s = startSlice; s < endSlice; s++)
        {
            if (s >= shaper.SliceGrabbers.Count) break;
            var slice = shaper.SliceGrabbers[s];

            ApplyScaleLogic(slice.Destinations, axis, pivot, scale);
            if (slice.OuterDestinations != null) ApplyScaleLogic(slice.OuterDestinations, axis, pivot, scale);
            if (slice.InnerDestinations != null) ApplyScaleLogic(slice.InnerDestinations, axis, pivot, scale);
        }
    }

    /*
    private void ApplyScaleLogic(List<Vector3> list, AxisCut axis, float pivot, float scale)
    {
        if (list == null) return;
        for (int k = 0; k < list.Count; k++)
        {
            Vector3 pos = list[k];
            float currentVal = GetAxisValue(pos, axis);

            // Formula: Pivot + (Distance * Scale)
            float newVal = pivot + ((currentVal - pivot) * scale);

            list[k] = SetAxisValue(pos, axis, newVal);
        }
    }*/

    private void ApplyScaleLogic(List<Vector3> list, AxisCut axis, float pivot, float scale)
    {
        if (list == null) return;
        for (int k = 0; k < list.Count; k++)
        {
            Vector3 pos = list[k];
            float currentVal = GetAxisValue(pos, axis);

            // Formula: Pivot + (Distance * Scale)
            // Distance (currentVal - pivot) preserves the sign (Above/Below)
            float newVal = pivot + ((currentVal - pivot) * scale);

            list[k] = SetAxisValue(pos, axis, newVal);
        }
    }

    // Moves an entire part along the axis without scaling
    private void ShiftPart(int partIndex, AxisCut axis, float amount)
    {
        int start = GetPartStartIndex(partIndex);
        int end = start + slicer.PartData[partIndex];

        for (int s = start; s < end; s++)
        {
            if (s >= shaper.SliceGrabbers.Count) break;
            var slice = shaper.SliceGrabbers[s];

            ApplyShiftLogic(slice.Destinations, axis, amount);
            if (slice.OuterDestinations != null) ApplyShiftLogic(slice.OuterDestinations, axis, amount);
            if (slice.InnerDestinations != null) ApplyShiftLogic(slice.InnerDestinations, axis, amount);
        }
    }

    private void ShiftPart(int start, int end, AxisCut axis, float amount)
    {
        for (int s = start; s < end; s++)
        {
            if (s >= shaper.SliceGrabbers.Count) break;
            var slice = shaper.SliceGrabbers[s];

            ApplyShiftLogic(slice.Destinations, axis, amount);
            if (slice.OuterDestinations != null) ApplyShiftLogic(slice.OuterDestinations, axis, amount);
            if (slice.InnerDestinations != null) ApplyShiftLogic(slice.InnerDestinations, axis, amount);
        }
    }

    private void ApplyShiftLogic(List<Vector3> list, AxisCut axis, float amount)
    {
        if (list == null) return;
        for (int k = 0; k < list.Count; k++)
        {
            Vector3 pos = list[k];
            float currentVal = GetAxisValue(pos, axis);
            list[k] = SetAxisValue(pos, axis, currentVal + amount);
        }
    }

    private int GetPartStartIndex(int partIndex)
    {
        int index = 0;
        for (int i = 0; i < partIndex; i++)
        {
            index += slicer.PartData[i];
        }
        return index;
    }

    // --- Utility ---

    private float FindEdgePoint(int FirstIndex, int LastIndex, AxisCut axis, bool FindTop = true)
    {
        var sg = shaper.SliceGrabbers;
        float Edge = FindTop ? float.MinValue : float.MaxValue;

        // Clamp to ensure we don't go out of bounds
        int actualLast = Mathf.Min(LastIndex, sg.Count);

        for (int index = FirstIndex; index < actualLast; index++)
        {
            var sliceDestinations = sg[index].Destinations;
            if (sliceDestinations == null) continue;

            foreach (var final in sliceDestinations)
            {
                float val = GetAxisValue(final, axis);
                if (FindTop)
                {
                    if (val > Edge) Edge = val;
                }
                else
                {
                    if (val < Edge) Edge = val;
                }
            }
        }

        // Handle empty case
        if (Edge == float.MinValue || Edge == float.MaxValue) return 0f;
        return Edge;
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
