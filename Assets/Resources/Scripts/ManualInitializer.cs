using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//For this initializer, suppose that we initialize final positions of each slice manually
// -1.7022, 2.896793, 2.1175
// -1.7022, 2.733347, 2.185304
// -1.76092, 2.733347, 2.083598
// -1.64348, 2.733347, 2.083598
// -1.528995, 2.655736, 2.2175
// -1.875405, 2.655736, 2.2175
// -1.7022, 2.655736, 2.1175


[RequireComponent(typeof(SliceReshaper))]
public class ManualInitializer : SliceInitializer
{
    //These are manually added from editor
    [SerializeField]
    List<Vector3> FinalPositions;

    [SerializeField]
    SliceReshaper shaper;

    private void Start()
    {
        //FinalPositions = new List<Vector3>();
    }

    //Finals
    //-1.701, 2.7678, 2.2171 --2
    //-1.7996, 2.7556, 2.0618 --3
    //-1.5611, 2.733347, 2.0261 --4
    //-1.7022, 2.608, 2.1175 --7

    public override void InitializeSlices()
    {
        if (FinalPositions != null && shaper!=null)
        {
            int g = 0;
            for(var s= 0;s<shaper.SliceGrabbers.Count;s++)
            {
                shaper.SliceGrabbers[s].Destinations = new List<Vector3>();
                for (var gr=0; gr < shaper.SliceGrabbers[s].Grabbers.Count; gr++)
                {
                    shaper.SliceGrabbers[s].Destinations.Add(new Vector3(FinalPositions[g].x, FinalPositions[g].y, FinalPositions[g].z));
                    g++;
                }
            }
        }   
    }
}
