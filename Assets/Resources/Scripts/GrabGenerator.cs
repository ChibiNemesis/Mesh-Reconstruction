using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class GrabGenerator : MonoBehaviour
{
    [SerializeField]
    SoftbodyActor actor;

    [SerializeField]
    GameObject GrabberObject;

    void Start()
    {
        if (actor == null)
        {
            actor = GetComponent<SoftbodyActor>();
        }
        GenerateGrabbers();
    }

    void Update()
    {
        
    }

    //Generate Grabber for each particle
    void GenerateGrabbers()
    {
        var particles = actor.SharedSimulationMesh.Particles;
        var parent = new GameObject(this.gameObject.name + " Grabbers");
        foreach(var par in particles)
        {
            //WIP
            var currpos = this.transform.position;
            var loc = par.Position + (new float3(currpos.x, currpos.y, currpos.z));
            Instantiate(GrabberObject, new Vector3(loc[0],loc[1],loc[2]), new Quaternion(), parent.transform);
        }
    }
}
