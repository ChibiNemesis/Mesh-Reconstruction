using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CylinderConeSliceInitializer : SliceInitializer
{
    [SerializeField]
    SliceReshaper shaper;

    public override void InitializeSlices()
    {
        var High = -1000f;
        var HighIndex = 0;
        for (var s = 0; s < shaper.SliceGrabbers.Count; s++)
        {
            var g = shaper.SliceGrabbers[s];
            Bounds b = new Bounds();
            b.SetMinMax(g.Min, g.Max);
            
            float val = b.center.y;
            if (val > High)
            {
                High = val;
                HighIndex = s;
            }
        }

        var Centroid = CalculateCentroid(shaper.SliceGrabbers[HighIndex].Grabbers);
        for (var s = 0; s < shaper.SliceGrabbers.Count; s++)
        {
            shaper.SliceGrabbers[s].Destinations = new List<Vector3>();
            for (var s2 = 0; s2 < shaper.SliceGrabbers[s].Grabbers.Count; s2++)
            {
                if (s == HighIndex)
                {
                    shaper.SliceGrabbers[s].Destinations.Add(new Vector3(Centroid.x, Centroid.y, Centroid.z));
                }
                else
                {
                    var pos = shaper.SliceGrabbers[s].Grabbers[s2].transform.position;
                    shaper.SliceGrabbers[s].Destinations.Add(new Vector3(pos.x, pos.y, pos.z));
                }
            }
        }
    }

    private Vector3 CalculateCentroid(List<GameObject> Grabbers)
    {
        Vector3 Sum = new Vector3(0f, 0f, 0f);
        foreach (var g in Grabbers)
        {
            var pos = g.transform.position;
            Sum += pos;
        }
        //var c = Sum / Grabbers.Count;
        return Sum / Grabbers.Count;
        //return new Vector3(BoundsCenter.x, c.y, BoundsCenter.z);
    }
}
