using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

//Store an AABB's Slice coordinates and particle grabbers
[System.Serializable, Inspectable]
public class SliceData
{
    public bool IsEdgeSlice;
    public Vector3 Min;
    public Vector3 Max;

    public List<GameObject> Grabbers;

    public List<GameObject> OuterGrabbers; // used on all slices
    public List<GameObject> InnerGrabbers; // used on first and last slice only

    //List of final dentinations for each particle
    public List<Vector3> Destinations;

    //Used for first and last slices
    public List<Vector3> OuterDestinations;
    public List<Vector3> InnerDestinations;

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