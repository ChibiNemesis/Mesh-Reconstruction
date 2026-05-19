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
        else if (mappingMode == InternalMappingMode.Directional)
        {
            MapInternalMeshDirectional(slice, axis, Contour);
        }
        else if (mappingMode == InternalMappingMode.Proximity)
        {
            MapInternalMeshProximity(slice, axis, Contour);
        }
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

    private void MapInternalMeshDirectional(SliceData slice, AxisCut axis, MeshCollider Contour)
    {
        if (slice.InnerGrabbers == null || slice.InnerGrabbers.Count == 0 || Contour == null) return;

        var InnerNew = new List<Vector3>();

        // 1. Calculate the DEFORMED Centroid (Where the ray will start)
        Vector3 deformedCentroid = Vector3.zero;
        if (slice.OuterDestinations != null && slice.OuterDestinations.Count > 0)
        {
            for (int i = 0; i < slice.OuterDestinations.Count; i++)
            {
                deformedCentroid += slice.OuterDestinations[i];
            }
            deformedCentroid /= slice.OuterDestinations.Count;
        }

        // 2. Calculate the UNDEFORMED Centroid (Used purely to find the correct mathematical angle)
        Vector3 undeformedCentroid = Vector3.zero;
        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            undeformedCentroid += slice.InnerGrabbers[i].transform.position;
        }
        undeformedCentroid /= slice.InnerGrabbers.Count;

        // 3. Perform the corrected Raycast
        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            Vector3 vertexPos = slice.InnerGrabbers[i].transform.position;

            // FIX: We calculate the direction using ONLY undeformed space.
            // This guarantees the ray perfectly matches the outward curve of the original 3D model.
            Vector3 direction = (vertexPos - undeformedCentroid).normalized;

            Vector3 finalPos = slice.InnerDestinations[i]; // Default fallback

            // Primary Raycast: From the new center, along the pure outward angle
            Ray outwardRay = new Ray(deformedCentroid, direction);

            if (Contour.Raycast(outwardRay, out RaycastHit hit, 1000f))
            {
                finalPos = hit.point;
            }
            else
            {
                // FALLBACK: Cast a reverse ray from outside-in to guarantee a hit
                Ray inwardRay = new Ray(deformedCentroid + (direction * 50f), -direction);
                if (Contour.Raycast(inwardRay, out RaycastHit reverseHit, 50f))
                {
                    finalPos = reverseHit.point;
                }
            }

            InnerNew.Add(finalPos);
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

    /// <summary>
    /// Uses Nearest-Neighbor surface snapping. Ideal for complex, non-convex geometries 
    /// (like the condyles of a femur) where radial centroid-raycasting fails.
    /// </summary>
    /*private void MapInternalMeshProximity(SliceData slice, AxisCut axis, MeshCollider Contour)
    {
        if (slice.InnerGrabbers == null || slice.InnerGrabbers.Count == 0 || Contour == null) return;

        var InnerNew = new List<Vector3>();

        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            // 1. Get the undeformed world position of the Grabber
            //Vector3 vertexPos = slice.InnerGrabbers[i].transform.position;
            Vector3 vertexPos = slice.InnerDestinations[i]; // Use the original inner destination as the starting point for proximity mapping

            // 2. Find the absolute closest point on the surface of the Contour mesh
            // This acts like a mathematical shrinkwrap, pulling the vertex straight to the surface
            // regardless of centroids, normals, or grazing angles.
            Vector3 closestSurfacePoint = Contour.ClosestPoint(vertexPos);

            // 3. Fallback: If ClosestPoint fails (e.g., if the point is exactly inside a non-convex collider),
            // it sometimes returns the original point. We can use an inward raycast as a safety net.
            if (Vector3.Distance(vertexPos, closestSurfacePoint) < 0.001f)
            {
                // Safety net: Cast a ray from the fat vertex INWARD toward the center of the contour bounds
                Vector3 inwardDir = (Contour.bounds.center - vertexPos).normalized;
                Ray inwardRay = new Ray(vertexPos - (inwardDir * 10f), inwardDir);

                if (Contour.Raycast(inwardRay, out RaycastHit hit, 50f))
                {
                    closestSurfacePoint = hit.point;
                }
                else
                {
                    closestSurfacePoint = slice.InnerDestinations[i]; // Final fallback
                }
            }

            InnerNew.Add(closestSurfacePoint);
        }

        slice.InnerDestinations = InnerNew;
    }*/

    private void MapInternalMeshProximity(SliceData slice, AxisCut axis, MeshCollider Contour)
    {
        // Safety check: We now require access to the sharedMesh data
        if (Contour == null || Contour.sharedMesh == null) return;

        // 1. SHRINKWRAP INNER NODES
        // Since you assigned 100% of the cap Grabbers to this list, they will all be mapped here perfectly.
        if (slice.InnerGrabbers != null && slice.InnerGrabbers.Count > 0)
        {
            var InnerNew = new List<Vector3>();
            for (int i = 0; i < slice.InnerGrabbers.Count; i++)
            {
                // We use the InnerDestinations (which already have the Locked Axis scaling applied)
                Vector3 originalPos = slice.InnerDestinations[i];

                // Bypass Physics entirely and use pure mathematical proximity
                Vector3 finalPos = GetClosestVertexOnMesh(originalPos, Contour);

                InnerNew.Add(finalPos);
            }
            slice.InnerDestinations = InnerNew;
        }

        // 2. SHRINKWRAP OUTER EDGES (If you still have any assigned to the outer boundary)
        if (slice.Destinations != null && slice.Destinations.Count > 0)
        {
            for (int i = 0; i < slice.Destinations.Count; i++)
            {
                Vector3 originalPos = slice.Destinations[i];
                Vector3 finalPos = GetClosestVertexOnMesh(originalPos, Contour);

                // Planar constraint for the outer seam to prevent tearing with adjacent slices
                switch (axis)
                {
                    case AxisCut.X: finalPos.x = originalPos.x; break;
                    case AxisCut.Y: finalPos.y = originalPos.y; break;
                    case AxisCut.Z: finalPos.z = originalPos.z; break;
                }
                slice.Destinations[i] = finalPos;
            }
        }
    }

    /// <summary>
    /// Bypasses Unity Physics to mathematically find the absolute closest vertex on the target mesh.
    /// This solves all issues with non-convex colliders and inside-out backface culling.
    /// </summary>
    private Vector3 GetClosestVertexOnMesh(Vector3 targetPoint, MeshCollider collider)
    {
        Transform meshTransform = collider.transform;
        Mesh mesh = collider.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        float minDistanceSqr = float.MaxValue;
        Vector3 closestWorldVertex = targetPoint;

        // Iterate through all vertices on the target contour to find the absolute closest one
        for (int i = 0; i < vertices.Length; i++)
        {
            // Convert local vertex position to world space
            Vector3 worldVertex = meshTransform.TransformPoint(vertices[i]);

            // Use sqrMagnitude for high performance (avoids heavy square root calculations)
            float distSqr = (targetPoint - worldVertex).sqrMagnitude;

            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                closestWorldVertex = worldVertex;
            }
        }

        return closestWorldVertex;
    }

    /*private void MapInternalMeshProximity(SliceData slice, AxisCut axis, MeshCollider Contour)
    {
        if (Contour == null) return;

        // The exact geometric center of the target contour mesh
        Vector3 contourCenter = Contour.bounds.center;

        // 1. SHRINKWRAP THE INNER NODES
        if (slice.InnerGrabbers != null && slice.InnerGrabbers.Count > 0)
        {
            var InnerNew = new List<Vector3>();
            for (int i = 0; i < slice.InnerGrabbers.Count; i++)
            {
                Vector3 originalPos = slice.InnerDestinations[i];
                Vector3 finalPos = CalculateShrinkwrapPosition(originalPos, contourCenter, Contour);
                InnerNew.Add(finalPos);
            }
            slice.InnerDestinations = InnerNew;
        }

        // 2. SHRINKWRAP THE OUTER EDGES (This fixes the fat caps!)
        // If your script uses a different list name for the edges, update it here.
        if (slice.InnerDestinations != null && slice.InnerDestinations.Count > 0)
        {
            for (int i = 0; i < slice.InnerDestinations.Count; i++)
            {
                Vector3 originalPos = slice.InnerDestinations[i];
                Vector3 finalPos = CalculateShrinkwrapPosition(originalPos, contourCenter, Contour);

                // Optional: Clamp the outer edge to the slice axis to prevent overlapping artifacts
                switch (axis)
                {
                    case AxisCut.X: finalPos.x = originalPos.x; break;
                    case AxisCut.Y: finalPos.y = originalPos.y; break;
                    case AxisCut.Z: finalPos.z = originalPos.z; break;
                }

                slice.InnerDestinations[i] = finalPos;
            }
        }
    }*/

    /// <summary>
    /// A custom, robust shrinkwrap function that avoids Unity's non-convex ClosestPoint bug.
    /// </summary>
    private Vector3 CalculateShrinkwrapPosition(Vector3 startPos, Vector3 targetCenter, MeshCollider contour)
    {
        // Vector pointing from the vertex INWARD to the center of the bone
        Vector3 inwardDir = (targetCenter - startPos).normalized;

        // Try 1: The "Fat" Check (Shoot inward from outside the bone)
        // We pull the ray back slightly to ensure it starts outside the mesh
        Ray inwardRay = new Ray(startPos - (inwardDir * 5f), inwardDir);
        if (contour.Raycast(inwardRay, out RaycastHit hitIn, 100f))
        {
            return hitIn.point;
        }

        // Try 2: The "Thin" Check (Shoot outward from inside the bone)
        // If the inward ray failed, the point might be trapped inside the bone.
        // We shoot from the center of the bone OUTWARD through the vertex.
        Vector3 outwardDir = (startPos - targetCenter).normalized;
        Ray outwardRay = new Ray(targetCenter, outwardDir);
        if (contour.Raycast(outwardRay, out RaycastHit hitOut, 100f))
        {
            return hitOut.point;
        }

        // Try 3: Total Fallback
        return startPos;
    }
}
