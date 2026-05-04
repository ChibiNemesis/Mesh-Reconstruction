using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Initializes and aligns planar contour data for sliced 3D models, supporting raycast-based adjustment and barycentric
/// mapping of Vertex points.
/// </summary>
/// <remarks>Used in medical or modeling applications to synchronize a source model's slices with target contour
/// data, leveraging mesh colliders and internal mesh logic for accurate positioning.</remarks>
public class ContourInitializerV2 : SliceInitializer
{
    [Header("References")]
    [SerializeField] SliceReshaper shaper;
    [SerializeField] BoundsSlicer slicer;
    // Helper to use Barycentric logic
    [SerializeField] InternalMeshHandler internalMeshHandler;

    // Helper to handle missing contours (in case a contour is missing/corrupted)
    [SerializeField] MissingContourHandler missingContourHandler;

    [Header("Target Data")]
    // The Patient Model, pre-sliced into parts. 
    // Index 0 must match Slice 0 of the Initial Model.
    [SerializeField] List<MeshCollider> TargetSliceColliders;

    [Header("Debug")]
    [SerializeField] bool ShowGizmos = true;
    [SerializeField] float GizmoRadius = 0.005f;

    [Header("Contour Data")]
    //GameObject containing planar contours
    [SerializeField] GameObject Contour;
    [SerializeField] public List<MeshFilter> ContourSlices;

    private GameObject SpawnedContour;

    /// <summary>
    /// Initializes contour data by instantiating the contour object, collecting mesh filters and mesh colliders from
    /// its children, and aligning the model to target positions.
    /// </summary>
    public void InitializeContourData()
    {
        ContourSlices = new List<MeshFilter>();
        TargetSliceColliders = new List<MeshCollider>();

        //Spawn a copy of the contour
        SpawnedContour = Instantiate(Contour, transform.position, transform.rotation);
        Debug.Assert(SpawnedContour != null, "Failed to spawn Contour. Please check the reference.");

        for (int c = 0; c < SpawnedContour.transform.childCount; c++)
        {
            var mesh = SpawnedContour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>();
            var Collider = SpawnedContour.transform.GetChild(c).gameObject.GetComponent<MeshCollider>();
            if (mesh != null && Collider == null) { Debug.LogWarning($"Child {c} of Contour is missing MeshCollider."); }

            //Saves mesh and colliders on list for later use
            ContourSlices.Add(mesh);
            TargetSliceColliders.Add(Collider);
        }
        AlignModelToTargets();
    }

    /// <summary>
    /// Initializes and updates slice positions by performing raycasts against target colliders and mapping inner
    /// grabbers using barycentric coordinates.
    /// </summary>
    /// <remarks>Logs warnings or errors if required references are missing or if mismatches occur between
    /// slices and target colliders. Attempts to recover from raycast misses by checking neighboring
    /// colliders.</remarks>
    public override void InitializeSlices()
    {
        if (shaper == null || Contour == null)
        {
            Debug.LogWarning("Missing reference to SliceReshaper or Contour.");
            return;
        }

        var slices = shaper.SliceGrabbers;
        AxisCut axis = slicer.GetAxis();

        // Safety Check
        if (slices.Count != TargetSliceColliders.Count)
        {
            Debug.LogError($"Mismatch: {slices.Count} Physics Slices vs {TargetSliceColliders.Count} Target Parts.");
            return;
        }

        int lastFoundIndex = -1;
        Vector3 lastFoundCentroid = Vector3.zero;
        int missingCount = 0;

        for (int i = 0; i < slices.Count; i++)
        {
            SliceData slice = slices[i];
            MeshCollider targetPart = TargetSliceColliders[i];

            // 0. Safety Check: Ensure OuterDestinations exists
            // (AdjustLockedAxis should have initialized and scaled these)
            if (slice.OuterDestinations == null || slice.OuterDestinations.Count == 0)
            {
                Debug.LogWarning($"Slice {i} has no OuterDestinations. Skipping.");
                continue;
            }

            // 1. Calculate Centroid using the scaled positions (OuterDestinations)
            Vector3 sliceCentroid = CalculateCentroid(slice.OuterDestinations);

            // Handle missing target colliders: count gaps and defer handling until next found
            if (targetPart == null)
            {
                // Increment missing count and move on; if we haven't found any previous contour, we'll still
                // wait until the next found contour to interpolate.
                missingCount++;
                continue;
            }

            // If we have a found collider and there were missing contours before it, attempt to fill the gap
            if (lastFoundIndex != -1 && missingCount > 0)
            {
                if (missingContourHandler != null)
                {
                    missingContourHandler.HandleMissingContours(slices, lastFoundIndex, i, lastFoundCentroid, sliceCentroid, missingCount);
                }
                else
                {
                    Debug.LogWarning($"MissingContourHandler not assigned. Cannot fill {missingCount} missing contours between {lastFoundIndex} and {i}.");
                }

                // Reset missing counter after handling
                missingCount = 0;
            }

            // Update last found index/centroid
            lastFoundIndex = i;
            lastFoundCentroid = sliceCentroid;

            // 2. Raycast using OuterDestinations as the guide
            for (int k = 0; k < slice.OuterDestinations.Count; k++)
            {
                // The current Scaled Position
                Vector3 currentPos = slice.OuterDestinations[k];

                // A. Adjust Centroid Height
                // Force the ray to be planar on the 'currentPos' height (which is the Scaled Height)
                float currentHeight = GetAxisValue(currentPos, axis);
                Vector3 rayOrigin = SetAxisValue(sliceCentroid, axis, currentHeight);

                // B. Calculate Direction (Center -> Scaled Position)
                Vector3 dir = (currentPos - rayOrigin).normalized;

                // Determine if this is a "Cap" slice
                bool isTopCap = (i == 0); // Assuming ascending order
                bool isBottomCap = (i == slices.Count - 1);

                // CHANGE: For Caps, shift the origin to angle the rays
                if (isTopCap || isBottomCap)
                {
                    // A. Adjust Centroid Height
                    currentHeight = GetAxisValue(currentPos, axis);
                    rayOrigin = SetAxisValue(sliceCentroid, axis, currentHeight);

                    // B. Calculate Direction (Center -> Scaled Position)
                    // Keep it purely horizontal!
                    dir = (currentPos - rayOrigin).normalized;
                }

                // C. Raycast
                Ray ray = new Ray(rayOrigin, dir);
                RaycastHit hit;

                // Cast far enough to hit the shell even if it expanded significantly
                bool hitFound = false;
                Vector3 finalPos = Vector3.zero;

                // Primary attempt: target collider for this slice
                if (targetPart != null && targetPart.Raycast(ray, out hit, 100f))
                {
                    hitFound = true;
                    finalPos = hit.point;
                }
                else
                {
                    // If primary missed, attempt neighboring slice colliders (previous then next)
                    int[] neighborIndices = new int[] { i - 1, i + 1 };
                    foreach (int ni in neighborIndices)
                    {
                        if (ni < 0 || ni >= TargetSliceColliders.Count) continue;
                        var neighborCollider = TargetSliceColliders[ni];
                        if (neighborCollider == null) continue;

                        if (neighborCollider.Raycast(ray, out hit, 100f))
                        {
                            hitFound = true;
                            finalPos = hit.point;
                            //Debug.Log($"Raycast for Slice {i}, Grabber {k} hit neighbor collider {ni}.");
                            break;
                        }
                    }
                }

                if (hitFound)
                {
                    // D. Update the LOCAL list (OuterDestinations)
                    slice.OuterDestinations[k] = finalPos;

                    // E. Update the MAIN Physics list (Destinations)
                    // We must find the original grabber to know which index to update in the main list
                    GameObject grabber = null;
                    if (slice.OuterGrabbers != null && k < slice.OuterGrabbers.Count)
                        grabber = slice.OuterGrabbers[k];

                    if (grabber != null)
                    {
                        int mainIndex = slice.Grabbers.IndexOf(grabber);
                        
                        /*if (mainIndex != -1)
                        {
                            slice.Destinations[mainIndex] = finalPos;
                        }*/
                    }
                }
                else
                {
                    // Debugging misses
                    Debug.DrawRay(rayOrigin, dir * 5f, Color.red, 2f);
                    Debug.LogWarning($"Raycast miss for Slice {i}, Grabber {k}. Tried slice {i} and neighbors. Ray Origin: {rayOrigin}, Direction: {dir}");
                }
            }

            // 4. Process Inner Grabbers (Barycentric Mapping)

            //Check if current slice's inner Grabbers and Destinations lists are valid
            if (slice.InnerGrabbers != null || slice.InnerDestinations != null)
            {
                //If they are valid, then triangulate this slice's outer grabbers and map the inner grabbers using barycentric coordinates
                slice.Triangulate();
                if (!internalMeshHandler)
                {
                    Debug.LogWarning("Missing reference to InternalMeshHandler. Cannot perform barycentric mapping for inner grabbers.");
                }
                else
                {
                    {
                        internalMeshHandler.MapInternalMesh(slice, axis);
                    }
                }
            }
        }
    }

    // --- Helpers ---

    // Aligns the model to the center of the target slice colliders by calculating their average position and moving the
    public void AlignModelToTargets()
    {
        if (TargetSliceColliders == null || TargetSliceColliders.Count == 0) return;

        // 1. Calculate Center of Target Data
        Vector3 targetCenter = Vector3.zero;
        foreach (var c in TargetSliceColliders)
        {
            // If a collider is missing, treat its center as Vector3.zero to avoid runtime errors.
            if (c == null)
                targetCenter += Vector3.zero;
            else
                targetCenter += c.bounds.center;
        }
        targetCenter /= TargetSliceColliders.Count;

        // 2. Move THIS gameobject to that center
        // (Assuming the script is on the Initial Model)
        transform.position = targetCenter;

        // 3. Force update of Slicer/Shaper since position changed
        // Might need to re-run Slicer.GetSlices() if it caches world positions
    }

    // Calculates the centroid of a list of points by averaging their positions.
    private Vector3 CalculateCentroid(List<Vector3> points)
    {
        Vector3 c = Vector3.zero;
        foreach (var p in points) c += p;
        return c / points.Count;
    }

    // Gets the value of the specified axis from a Vector3.
    private float GetAxisValue(Vector3 v, AxisCut axis)
    {
        return axis switch { AxisCut.X => v.x, AxisCut.Y => v.y, AxisCut.Z => v.z, _ => v.y };
    }

    // Sets the value of the specified axis in a Vector3 and returns the modified vector.
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

    // Draws gizmos in the editor to visualize the raycast results and barycentric mapping for outer and inner grabbers.
    void OnDrawGizmos()
    {
        if (!ShowGizmos || shaper == null || shaper.SliceGrabbers == null) return;

        foreach (var slice in shaper.SliceGrabbers)
        {
            // 1. Draw Outer Destinations (Green) - Result of Raycasting
            if (slice.OuterDestinations != null)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < slice.OuterDestinations.Count; i++)
                {
                    Vector3 finalPos = slice.OuterDestinations[i];
                    Gizmos.DrawSphere(finalPos, GizmoRadius);

                    // Optional: Draw line from original grabber to final pos
                    if (slice.OuterGrabbers != null && i < slice.OuterGrabbers.Count)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(slice.OuterGrabbers[i].transform.position, finalPos);
                        Gizmos.color = Color.green; // Reset
                    }
                }
            }

            // 2. Draw Inner Destinations (Red) - Result of Barycentric Mapping
            if (slice.InnerDestinations != null)
            {
                Gizmos.color = Color.red;
                for (int i = 0; i < slice.InnerDestinations.Count; i++)
                {
                    Vector3 finalPos = slice.InnerDestinations[i];
                    Gizmos.DrawSphere(finalPos, GizmoRadius);

                    // Optional: Draw line from original inner grabber
                    if (slice.InnerGrabbers != null && i < slice.InnerGrabbers.Count)
                    {
                        Gizmos.color = new Color(1, 0.5f, 0); // Orange
                        Gizmos.DrawLine(slice.InnerGrabbers[i].transform.position, finalPos);
                        Gizmos.color = Color.red; // Reset
                    }
                }
            }
        }
    }
}
