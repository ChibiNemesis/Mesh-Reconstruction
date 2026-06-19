using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enum to define which mapping methodology to use for internal vertices
public enum InternalMappingMode
{
    Barycentric,
    Directional,
    Proximity, // ADDED: Best for complex, non-convex caps
    BoundsMatch
}

/// <summary>
/// Handles internal mesh mapping and manipulation for slice data within a Unity scene.
/// </summary>
/// <remarks>Intended for internal use in mesh processing workflows, particularly for updating inner mesh
/// destinations based on barycentric coordinates and axis constraints.</remarks>
public class InternalMeshHandler : MonoBehaviour
{
    [Header("Mapping Settings")]
    [SerializeField]
    private InternalMappingMode mappingMode = InternalMappingMode.Barycentric;

    /// <summary>
    /// Maps the internal mesh vertices of the specified slice to new positions based on barycentric coordinates and the
    /// given axis constraint.
    /// </summary>
    /// <param name="slice">The slice containing mesh and grabber data to be mapped.</param>
    /// <param name="axis">The axis along which the internal vertex positions are preserved.</param>
    public void MapInternalMesh(SliceData slice,AxisCut axis, MeshCollider Contour)
    {
        if (mappingMode == InternalMappingMode.Barycentric)
        {
            MapInternalMeshBarycentric(slice, axis);
        }
        /*else if (mappingMode == InternalMappingMode.Directional)
        {
            MapInternalMeshDirectional(slice, axis, Contour);
        }
        else if (mappingMode == InternalMappingMode.Proximity)
        {
            MapInternalMeshProximity(slice, axis, Contour);
        }*/
        else if (mappingMode == InternalMappingMode.BoundsMatch)
        {
            MatchCapBounds(slice, Contour, axis);
        }
    }

    private void MapInternalMeshBarycentric(SliceData slice, AxisCut axis)
    {
        // 1. Safety Checks
        if (slice.InnerGrabbers == null || slice.InnerGrabbers.Count == 0) return;

        var InnerNew = new List<Vector3>();

        slice.Triangulate();

        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            var pg = slice.InnerGrabbers[i].GetComponent<ParticleGrab>();

            Vector3 B_Coords;
            B_Coords = pg.GetBarycentricCoordinates(); // Barycentric Coordinates
            Debug.Assert(pg.TriangleIndices != null);
            Debug.Assert(pg.TriangleIndices.Count == 3);
            Vector3 A = slice.OuterDestinations[pg.TriangleIndices[0]];
            Vector3 B = slice.OuterDestinations[pg.TriangleIndices[1]];
            Vector3 C = slice.OuterDestinations[pg.TriangleIndices[2]];

            // Q = x*A + y*B + z*C
            Vector3 InnerPos = (B_Coords.x * A) + (B_Coords.y * B) + (B_Coords.z * C);

            //write a small code segment that changes InnerPos's coordinate corresponding to the axis to the initial inner destination's coordinate for that axis
            switch (axis)
            {
                case AxisCut.X:
                    InnerPos.x = slice.InnerDestinations[i].x;
                    break;
                case AxisCut.Y:
                    InnerPos.y = slice.InnerDestinations[i].y;
                    break;
                case AxisCut.Z:
                    InnerPos.z = slice.InnerDestinations[i].z;
                    break;
            }

            InnerNew.Add(InnerPos);
        }
        slice.InnerDestinations = InnerNew;
    }

    /// <summary>
    /// Performs Anisotropic Bounding Box Registration. 
    /// Matches the volumetric bounds of the generic cap to the bounds of the target contour.
    /// </summary>
    public void MatchCapBounds(SliceData capSlice, MeshCollider targetContour, AxisCut lockedAxis)
    {
        if (capSlice.InnerGrabbers == null || capSlice.InnerGrabbers.Count == 0 || targetContour == null) return;

        // 1. Calculate Target Bounds (The Patient-Specific Contour)
        // Unity's collider bounds are already in absolute World Space.
        Bounds targetBounds = targetContour.bounds;

        // 2. Calculate Source Bounds (The Generic Cap)
        // We calculate this using the current InnerDestinations to account for any prior movement.
        Bounds sourceBounds = new Bounds(capSlice.InnerDestinations[0], Vector3.zero);
        for (int i = 1; i < capSlice.InnerGrabbers.Count; i++)
        {
            sourceBounds.Encapsulate(capSlice.InnerDestinations[i]);
        }

        // 3. Calculate Scale Factors
        // Prevent divide-by-zero errors in case a bound is perfectly flat
        Vector3 scale = new Vector3(
            sourceBounds.size.x > 0.0001f ? targetBounds.size.x / sourceBounds.size.x : 1f,
            sourceBounds.size.y > 0.0001f ? targetBounds.size.y / sourceBounds.size.y : 1f,
            sourceBounds.size.z > 0.0001f ? targetBounds.size.z / sourceBounds.size.z : 1f
        );

        // OPTIONAL: Since the LockedAxisAdjuster already perfectly scaled the length of the bone, 
        // you usually want to lock that specific axis here so we don't double-scale the height.
        // We only want to scale the "fatness" (the cross-section).
        switch (lockedAxis)
        {
            case AxisCut.X: scale.x = 1f; break;
            case AxisCut.Y: scale.y = 1f; break;
            case AxisCut.Z: scale.z = 1f; break;
        }

        // 4. Apply Affine Transformation (Scale + Shift)
        var InnerNew = new List<Vector3>();
        for (int i = 0; i < capSlice.InnerGrabbers.Count; i++)
        {
            Vector3 pos = capSlice.InnerDestinations[i];

            // A. Localize the vertex relative to its own center
            Vector3 localPos = pos - sourceBounds.center;

            // B. Apply the Bounding Box Scale
            localPos.x *= scale.x;
            localPos.y *= scale.y;
            localPos.z *= scale.z;

            // C. Translate to the Target Contour's center
            Vector3 newPos = targetBounds.center + localPos;

            // D. Restore the locked axis position to maintain alignment with the rest of the bone shaft
            switch (lockedAxis)
            {
                case AxisCut.X: newPos.x = pos.x; break;
                case AxisCut.Y: newPos.y = pos.y; break;
                case AxisCut.Z: newPos.z = pos.z; break;
            }

            InnerNew.Add(newPos);
        }

        // Apply the newly calculated bounds-matched positions
        capSlice.InnerDestinations = InnerNew;
    }

}
