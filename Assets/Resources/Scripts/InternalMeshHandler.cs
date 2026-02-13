using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InternalMeshHandler : MonoBehaviour
{
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
            Vector3 A = slice.Destinations[pg.TriangleIndices[0]];
            Vector3 B = slice.Destinations[pg.TriangleIndices[1]];
            Vector3 C = slice.Destinations[pg.TriangleIndices[2]];

            // Q = x*A + y*B + z*C
            Vector3 InnerPos = (B_Coords.x * A) + (B_Coords.y * B) + (B_Coords.z * C);
            InnerNew.Add(InnerPos);
        }
        slice.InnerDestinations = InnerNew;
    }
}
