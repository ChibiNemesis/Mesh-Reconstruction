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

    public List<GameObject> Grabbers;

    void Start()
    {
        if (actor == null)
        {
            actor = GetComponent<SoftbodyActor>();
        }
        GenerateGrabbers();
    }


    //Generate Grabber for each particle
    void GenerateGrabbers()
    {
        var scale = transform.localScale;
        var particles = actor.SharedSimulationMesh.Particles;
        var parent = new GameObject(this.gameObject.name + " Grabbers");
        foreach(var par in particles)
        {
            var currpos = transform.position;
            var loc = (par.Position * (new float3(scale.x, scale.y, scale.z))) + (new float3(currpos.x, currpos.y, currpos.z));
            Grabbers.Add(Instantiate(GrabberObject, new Vector3(loc[0],loc[1],loc[2]), new Quaternion(), parent.transform));
        }
    }
}
