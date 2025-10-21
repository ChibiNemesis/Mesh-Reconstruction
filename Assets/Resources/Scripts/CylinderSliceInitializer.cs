using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CylinderSliceInitializer : SliceInitializer
{
    [SerializeField]
    SliceReshaper shaper;

    [SerializeField]
    float ArcOffset = 0.6f;

    public override void InitializeSlices()
    {
        var tmp = GetComponent<MeshFilter>().sharedMesh.bounds.center;
        var pos1 = transform.position;

        //Use XZ axis from here only and Y from function
        var BoundsCenter = new Vector3(tmp.x + pos1.x, tmp.y + pos1.y, tmp.z + pos1.z);

        for (var s = 0; s < shaper.SliceGrabbers.Count; s++)
        {
            shaper.SliceGrabbers[s].Destinations = new List<Vector3>();
            var Centroid = CalculateCentroid(shaper.SliceGrabbers[s].Grabbers, BoundsCenter);
            for (var p = 0; p < shaper.SliceGrabbers[s].Grabbers.Count; p++)
            {
                var pos = shaper.SliceGrabbers[s].Grabbers[p].transform.position;
                var direction = pos - Centroid;
                var dist_left = ArcOffset - Vector3.Distance(pos, Centroid);
                Debug.Log("Dist left:" + dist_left);
                shaper.SliceGrabbers[s].Destinations.Add(pos + direction * dist_left);
            }
        }
    }

    private Vector3 CalculateCentroid(List<GameObject> Grabbers, Vector3 BoundsCenter)
    {
        Vector3 Sum = new Vector3(0f, 0f, 0f);
        foreach (var g in Grabbers)
        {
            var pos = g.transform.position;
            Sum += pos;
        }
        var c = Sum / Grabbers.Count;

        return new Vector3(BoundsCenter.x, c.y, BoundsCenter.z);
    }
}
