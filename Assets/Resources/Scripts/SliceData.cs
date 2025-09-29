using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

//Store a AABB's Slice coordinates and particle grabbers
[System.Serializable, Inspectable]
public class SliceData
{
    public Vector3 Min;
    public Vector3 Max;

    public List<GameObject> Grabbers;

    //List of final dentinations for each particle
    public List<Vector3> Destinations;

    public SliceData()
    {
        Grabbers = new List<GameObject>();
        Min = Vector3.zero;
        Max = Vector3.zero;
    }

    public SliceData(Vector3 _min, Vector3 _max)
    {
        Grabbers = new List<GameObject>();
        Min = _min;
        Max = _max;
    }
}