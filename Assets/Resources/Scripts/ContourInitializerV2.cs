using System.Collections.Generic;
using UnityEngine;

public class ContourInitializerV2 : SliceInitializer
{
    [Header("References")]
    [SerializeField]
    SliceReshaper shaper;
    [SerializeField]
    BoundsSlicer slicer;

    [Header("Target Data")]
    // The Patient Model, pre-sliced into parts. 
    // Index 0 must match Slice 0 of the Initial Model.
    [SerializeField] List<MeshCollider> TargetSliceColliders;

    [Header("Debug")]
    [SerializeField] bool ShowGizmos = true;
    [SerializeField] float GizmoRadius = 0.005f;

    //GameObject containing planar contours
    [SerializeField]
    GameObject Contour;

    public List<MeshFilter> ContourSlices;

    // Helper to use Barycentric logic
    [SerializeField] InternalMeshHandler internalMeshHandler;

    private void Start()
    {
        InitializeContourData();
    }

    private void InitializeContourData()
    {
        ContourSlices = new List<MeshFilter>();
        TargetSliceColliders = new List<MeshCollider>();

        for (int c = 0; c < Contour.transform.childCount; c++)
        {
            var mesh = Contour.transform.GetChild(c).gameObject.GetComponent<MeshFilter>();
            var Collider = Contour.transform.GetChild(c).gameObject.GetComponent<MeshCollider>();
            if (mesh != null && Collider == null) { Debug.LogWarning($"Child {c} of Contour is missing MeshCollider."); }

            //Saves mesh and colliders on list for later use
            ContourSlices.Add(mesh);
            TargetSliceColliders.Add(Collider);
        }
        AlignModelToTargets();
    }

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

                // C. Raycast
                Ray ray = new Ray(rayOrigin, dir);
                RaycastHit hit;

                // Cast far enough to hit the shell even if it expanded significantly
                if (targetPart.Raycast(ray, out hit, 100f))
                {
                    Vector3 finalPos = hit.point;

                    // D. Update the LOCAL list (OuterDestinations)
                    slice.OuterDestinations[k] = finalPos;

                    // E. Update the MAIN Physics list (Destinations)
                    // We must find the original grabber to know which index to update in the main list
                    GameObject grabber = slice.OuterGrabbers[k];
                    int mainIndex = slice.Grabbers.IndexOf(grabber);

                    /*
                    if (mainIndex != -1)
                    {
                        slice.Destinations[mainIndex] = finalPos;
                    }*/
                }
                else
                {
                    // Debugging misses
                    Debug.DrawRay(rayOrigin, dir * 5f, Color.red, 2f);
                    Debug.LogWarning($"Raycast miss for Slice {i}, Grabber {k}. Ray Origin: {rayOrigin}, Direction: {dir}");
                }
            }

            // 4. Process Inner Grabbers (Barycentric Mapping)
            // We reuse the logic from previous steps. 
            // The Outer Grabbers now have their Final Positions set in slice.Destinations.
            // We treat those as the "Deformed Boundary" to map the inner points.
            if (internalMeshHandler != null && slice.InnerGrabbers.Count > 0)
            {
                // Ensure InnerDestinations is initialized
                if (slice.InnerDestinations == null || slice.InnerDestinations.Count != slice.InnerGrabbers.Count)
                {
                    slice.InnerDestinations = new List<Vector3>(new Vector3[slice.InnerGrabbers.Count]);
                }

                // This method triangulates OuterDestinations
                // and maps InnerGrabbers based on where they sit in the Initial Outer Polygon.
                internalMeshHandler.MapInternalMesh(slice, axis);
            }
        }
    }

    // --- Helpers ---

    public void AlignModelToTargets()
    {
        if (TargetSliceColliders == null || TargetSliceColliders.Count == 0) return;

        // 1. Calculate Center of Target Data
        Vector3 targetCenter = Vector3.zero;
        foreach (var c in TargetSliceColliders) targetCenter += c.bounds.center;
        targetCenter /= TargetSliceColliders.Count;

        // 2. Move THIS gameobject to that center
        // (Assuming the script is on the Initial Model)
        transform.position = targetCenter;

        // 3. Force update of Slicer/Shaper since position changed
        // You might need to re-run Slicer.GetSlices() if it caches world positions
    }

    private Vector3 CalculateCentroid(List<GameObject> grabbers)
    {
        if (grabbers == null || grabbers.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var g in grabbers)
        {
            sum += g.transform.position;
        }
        return sum / grabbers.Count;
    }

    private Vector3 CalculateCentroid(List<Vector3> points)
    {
        Vector3 c = Vector3.zero;
        foreach (var p in points) c += p;
        return c / points.Count;
    }

    private float GetAxisValue(Vector3 v, AxisCut axis)
    {
        return axis switch { AxisCut.X => v.x, AxisCut.Y => v.y, AxisCut.Z => v.z, _ => v.y };
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
