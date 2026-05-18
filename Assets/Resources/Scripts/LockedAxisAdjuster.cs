using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides functionality to adjust the scaling and positioning of mesh parts along a locked axis using specified scale
/// multipliers for each part.
/// </summary>
/// <remarks>Intended for use with objects that utilize SliceReshaper and BoundsSlicer components to manipulate
/// mesh geometry based on axis-aligned slicing and scaling operations.</remarks>
public class LockedAxisAdjuster : MonoBehaviour
{
    [SerializeField]
    SliceReshaper shaper;

    [SerializeField]
    BoundsSlicer slicer;

    //Scale multiplier for each part
    [SerializeField]
    List<float> PartScale;

    // Store original centers
    private float[] origCenter;

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

    // Main method to adjust the locked axis based on PartScale values
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

        // Initialize the array exactly when we need it, based on current part count
        origCenter = new float[TotalParts];

        // 2. Determine Strategy
        if (TotalParts == 1)
        {
            ScaleSingle(axis);
        }
        else if (TotalParts >= 2)
        {
            // Handles Double, Triple, and any multiple parts dynamically
            ProcessSequentialScaling(axis, TotalParts);
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

    // Universal Case: Double or Multiple Parts
    private void ProcessSequentialScaling(AxisCut axis, int totalParts)
    {
        // STEP 0: Pre-calculate the original centers and store them in the class field
        for (int i = 0; i < totalParts; i++)
        {
            int start = GetPartStartIndex(i);
            int end = start + slicer.PartData[i];
            float max = FindEdgePoint(start, end, axis, true);
            float min = FindEdgePoint(start, end, axis, false);
            origCenter[i] = (max + min) * 0.5f;
        }

        // STEP 1: Scale every part uniformly from its OWN local center.
        for (int i = 0; i < totalParts; i++)
        {
            float scale = PartScale[i];
            if (Mathf.Abs(scale - 1f) < 0.0001f) continue;

            int start = GetPartStartIndex(i);
            int end = start + slicer.PartData[i];
            ScalePartAroundPivot(start, end, axis, origCenter[i], scale);
        }

        // STEP 2: Find the Anchor Part
        int anchorIndex = 0;
        for (int i = 0; i < totalParts; i++)
        {
            if (Mathf.Abs(PartScale[i] - 1f) < 0.0001f)
            {
                anchorIndex = i;
                break;
            }
        }

        // STEP 3: Shift parts sequentially OUTWARDS from the anchor

        // A) Iterate upwards from the anchor
        for (int i = anchorIndex + 1; i < totalParts; i++)
        {
            SnapPartToAnchor(currIdx: i, prevIdx: i - 1, axis);
        }

        // B) Iterate downwards from the anchor
        for (int i = anchorIndex - 1; i >= 0; i--)
        {
            SnapPartToAnchor(currIdx: i, prevIdx: i + 1, axis);
        }
    }

    /*private void ProcessSequentialScaling(AxisCut axis, int totalParts)
    {
        // STEP 0: Pre-calculate the original centers and edges of every part
        // This is critical to determine their true spatial relationship before scaling.
        float[] origMin = new float[totalParts];
        float[] origMax = new float[totalParts];
        float[] origCenter = new float[totalParts];

        for (int i = 0; i < totalParts; i++)
        {
            int start = GetPartStartIndex(i);
            int end = start + slicer.PartData[i];
            origMax[i] = FindEdgePoint(start, end, axis, true);
            origMin[i] = FindEdgePoint(start, end, axis, false);
            origCenter[i] = (origMax[i] + origMin[i]) * 0.5f;
        }

        // STEP 1: Scale every part uniformly from its OWN local center.
        for (int i = 0; i < totalParts; i++)
        {
            float scale = PartScale[i];
            if (Mathf.Abs(scale - 1f) < 0.0001f) continue;

            int start = GetPartStartIndex(i);
            int end = start + slicer.PartData[i];
            ScalePartAroundPivot(start, end, axis, origCenter[i], scale);
        }

        // STEP 2: Find the Anchor Part
        // The Anchor is a part with scale 1.0. If none exist, default to part 0.
        int anchorIndex = 0;
        for (int i = 0; i < totalParts; i++)
        {
            if (Mathf.Abs(PartScale[i] - 1f) < 0.0001f)
            {
                anchorIndex = i;
                break;
            }
        }

        // STEP 3: Shift parts sequentially OUTWARDS from the anchor

        // A) Iterate upwards from the anchor
        for (int i = anchorIndex + 1; i < totalParts; i++)
        {
            SnapPartToAnchor(currIdx: i, prevIdx: i - 1, axis);
        }

        // B) Iterate downwards from the anchor
        for (int i = anchorIndex - 1; i >= 0; i--)
        {
            SnapPartToAnchor(currIdx: i, prevIdx: i + 1, axis);
        }
    }*/

    // --- Core Helpers ---

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
    }

    // Moves an entire part along the axis without scaling it
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

    /*private float FindEdgePoint(int FirstIndex, int LastIndex, AxisCut axis, bool FindTop = true)
    {
        var sg = shaper.SliceGrabbers;
        float Edge = FindTop ? float.MinValue : float.MaxValue;

        // Clamp to ensure we don't go out of bounds
        int actualLast = Mathf.Min(LastIndex, sg.Count);

        for (int index = FirstIndex; index < actualLast; index++)
        {
            // FIX: Use OuterDestinations instead of Destinations
            // OuterDestinations are guaranteed to exist and define the boundary
            var slicePoints = sg[index].OuterDestinations;

            // Fallback to Destinations if Outer is null (safety check)
            if (slicePoints == null || slicePoints.Count == 0)
                slicePoints = sg[index].Destinations;

            if (slicePoints == null || slicePoints.Count == 0) continue;

            foreach (var final in slicePoints)
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
    }*/

    // Helper method to safely snap two parts together regardless of their array order
    private void SnapPartToAnchor(int currIdx, int prevIdx, AxisCut axis)
    {
        // NO ERROR: This now cleanly accesses the class-level origCenter array!
        bool isCurrAbovePrev = origCenter[currIdx] > origCenter[prevIdx];

        int prevStart = GetPartStartIndex(prevIdx);
        int prevEnd = prevStart + slicer.PartData[prevIdx];
        int currStart = GetPartStartIndex(currIdx);
        int currEnd = currStart + slicer.PartData[currIdx];

        float shiftAmount = 0f;

        if (isCurrAbovePrev)
        {
            // Curr is higher up. Snap Curr's Min to Prev's Max.
            float newPrevMax = FindEdgePoint(prevStart, prevEnd, axis, true);
            float newCurrMin = FindEdgePoint(currStart, currEnd, axis, false);
            shiftAmount = newPrevMax - newCurrMin;
        }
        else
        {
            // Curr is lower down. Snap Curr's Max to Prev's Min.
            float newPrevMin = FindEdgePoint(prevStart, prevEnd, axis, false);
            float newCurrMax = FindEdgePoint(currStart, currEnd, axis, true);
            shiftAmount = newPrevMin - newCurrMax;
        }

        // Apply the closing shift
        if (Mathf.Abs(shiftAmount) > 0.0001f)
        {
            ShiftPart(currIdx, axis, shiftAmount);
        }
    }
    /*private void SnapPartToAnchor(int currIdx, int prevIdx, AxisCut axis)
    {
        // Mathematically determine which part sits higher on the axis
        bool isCurrAbovePrev = origCenter[currIdx] > origCenter[prevIdx];

        int prevStart = GetPartStartIndex(prevIdx);
        int prevEnd = prevStart + slicer.PartData[prevIdx];
        int currStart = GetPartStartIndex(currIdx);
        int currEnd = currStart + slicer.PartData[currIdx];

        float shiftAmount = 0f;

        if (isCurrAbovePrev)
        {
            // Curr is higher up. Snap Curr's Min to Prev's Max.
            float newPrevMax = FindEdgePoint(prevStart, prevEnd, axis, true);
            float newCurrMin = FindEdgePoint(currStart, currEnd, axis, false);
            shiftAmount = newPrevMax - newCurrMin;
        }
        else
        {
            // Curr is lower down. Snap Curr's Max to Prev's Min.
            float newPrevMin = FindEdgePoint(prevStart, prevEnd, axis, false);
            float newCurrMax = FindEdgePoint(currStart, currEnd, axis, true);
            shiftAmount = newPrevMin - newCurrMax;
        }

        // Apply the closing shift
        if (Mathf.Abs(shiftAmount) > 0.0001f)
        {
            ShiftPart(currIdx, axis, shiftAmount);
        }
    }*/

    private float FindEdgePoint(int FirstIndex, int LastIndex, AxisCut axis, bool FindTop = true)
    {
        var sg = shaper.SliceGrabbers;
        float Edge = FindTop ? float.MinValue : float.MaxValue;

        int actualLast = Mathf.Min(LastIndex, sg.Count);

        for (int index = FirstIndex; index < actualLast; index++)
        {
            var slicePoints = sg[index].OuterDestinations;

            if (slicePoints == null || slicePoints.Count == 0)
                slicePoints = sg[index].Destinations;

            if (slicePoints == null || slicePoints.Count == 0) continue;

            foreach (var final in slicePoints)
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
