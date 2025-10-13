using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(SliceReshaper))]
public class ManualInitializer : SliceInitializer
{
    //These are manually added from editor
    [SerializeField]
    List<Vector3> FinalPositions;

    [SerializeField]
    SliceReshaper shaper;

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
