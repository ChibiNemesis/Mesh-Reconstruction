using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Simple class for storing Min and Max points of a bounding box
public class BoundsPoints
{
    public Vector3 Min { set; get; }
    public Vector3 Max { set; get; }

    public BoundsPoints() { }
    public BoundsPoints(Vector3 _min, Vector3 _max) { 
        Min = new Vector3(_min.x, _min.y, _min.z); 
        Max = new Vector3(_max.x, _max.y, _max.z);
    }

    public BoundsPoints(BoundsPoints b)
    {
        Min = new Vector3(b.Min.x, b.Min.y, b.Min.z);
        Max = new Vector3(b.Max.x, b.Max.y, b.Max.z);
    }
}
