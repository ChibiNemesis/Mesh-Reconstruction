using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//For this experiment, suppose that there are 3 slices
//From the Initialized Slices
//Slice 0 is the Top
//Slice 1 is middle
//Slice 2 is Bottom
[RequireComponent(typeof(SliceReshaper))]
public class PyramidToD12 : SliceInitializer
{
    [SerializeField]
    public List<GameObject> MiddlePoints;

    [SerializeField]
    public GameObject MiddlePoint;

    [SerializeField]
    public SliceReshaper shaper;

    [SerializeField]
    private float x_correction, y_correction, z_correction;

    private Vector3 Center;

    void Start()
    {
        float x, y, z;
        x = transform.position.x;
        y = transform.position.y;
        z = transform.position.z;
        Center = gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.center;
        Center.x += x_correction + x;
        Center.y += y_correction + y;
        Center.z += z_correction + z;
        //Debug.Log(Center);
    }
    public override void InitializeSlices()
    {
        foreach(var Slice in shaper.SliceGrabbers)
        {
            if (Slice.Destinations == null)
            {
                Slice.Destinations = new List<Vector3>();
            }
            //Initialize all slices
            for(var g = 0; g < Slice.Grabbers.Count; g++)
            {
                var pos = Slice.Grabbers[g].transform.position;
                Slice.Destinations.Add(new Vector3(pos.x, pos.y, pos.z));
            }
        }

        //First Move the Top point
        var TopSlice = shaper.SliceGrabbers[0];
        var dest = TopSlice.Destinations[0];

        dest.x += x_correction;
        dest.y += y_correction;
        dest.z += z_correction;

        //Then move middle points outwards
        var MiddleSlice = shaper.SliceGrabbers[1];
        for(int g = 0; g < MiddleSlice.Grabbers.Count; g++)
        {
            var gr = MiddleSlice.Grabbers[g].transform.position;
            var direction = gr - Center;
            MiddleSlice.Destinations[g] += 0.4f*direction;

            //MiddleSlice.Destinations[g];
        }
    }
}
