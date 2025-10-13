using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SliceReshaper))]
public class DirectionInitializer : SliceInitializer
{
    //Deform on a certain direction from center outwards to specific vertices
    [SerializeField]
    List<int> DeformIndices;

    [SerializeField]
    SliceReshaper shaper;

    private Vector3 Center;

    private void Start()
    {
        var C = GetComponent<MeshFilter>().sharedMesh.bounds.center;
        var pos = transform.position;
        Center = new Vector3(C.x + pos.x, C.y + pos.y, C.z + pos.z);
        Debug.Log("Center: "+Center);
    }

    public override void InitializeSlices()
    {
        var count = 0;
        for(var s = 0; s < shaper.SliceGrabbers.Count; s++)
        {
            shaper.SliceGrabbers[s].Destinations = new List<Vector3>();
            for (var p = 0; p < shaper.SliceGrabbers[s].Grabbers.Count; p++)
            {
                var pos = shaper.SliceGrabbers[s].Grabbers[p].transform.position;
                if (DeformIndices.Contains(count))
                {
                    var direction = pos - Center;
                    var newPos = pos + direction * 0.6f;
                    shaper.SliceGrabbers[s].Destinations.Add(new Vector3(newPos.x, newPos.y, newPos.z));
                }
                else
                {
                    shaper.SliceGrabbers[s].Destinations.Add(new Vector3(pos.x, pos.y, pos.z));
                }
                
                count++;
            }
        }

    }
}
