using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles internal mesh mapping and manipulation for slice data within a Unity scene.
/// </summary>
/// <remarks>Intended for internal use in mesh processing workflows, particularly for updating inner mesh
/// destinations based on barycentric coordinates and axis constraints.</remarks>
public class InternalMeshHandler : MonoBehaviour
{
    /// <summary>
    /// Maps the internal mesh vertices of the specified slice to new positions based on barycentric coordinates and the
    /// given axis constraint.
    /// </summary>
    /// <param name="slice">The slice containing mesh and grabber data to be mapped.</param>
    /// <param name="axis">The axis along which the internal vertex positions are preserved.</param>
    public void MapInternalMesh(SliceData slice,AxisCut axis)
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
}
