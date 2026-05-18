using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Enum to define which mapping methodology to use for internal vertices
public enum InternalMappingMode
{
    Barycentric,
    Directional,
    Proximity // ADDED: Best for complex, non-convex caps
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
    /// Uses Nearest-Neighbor surface snapping. Ideal for complex, non-convex geometries 
    /// (like the condyles of a femur) where radial centroid-raycasting fails.
    /// </summary>
    private void MapInternalMeshProximity(SliceData slice, AxisCut axis, MeshCollider Contour)
    {
        if (slice.InnerGrabbers == null || slice.InnerGrabbers.Count == 0 || Contour == null) return;

        var InnerNew = new List<Vector3>();

        for (int i = 0; i < slice.InnerGrabbers.Count; i++)
        {
            // 1. Get the undeformed world position of the Grabber
            Vector3 vertexPos = slice.InnerGrabbers[i].transform.position;

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
    }
}
