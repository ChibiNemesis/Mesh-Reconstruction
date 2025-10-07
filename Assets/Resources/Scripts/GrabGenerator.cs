using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


public class GrabGenerator : MonoBehaviour
{
    [SerializeField]
    BasePhysicsActor actor;

    [SerializeField]
    GameObject GrabberObject;

    public List<GameObject> Grabbers { set; get; }

    private bool IsInitialized = false;

    void Start()
    {
        if (actor == null)
        {
            actor = GetComponent<BasePhysicsActor>();
        }
    }

    //Generate Grabber for each particle
    public void GenerateGrabbers()
    {
        if (IsInitialized)
            return;
        Grabbers = new List<GameObject>();
        if (actor == null)
        {
            Debug.LogWarning("Could not find Soft body actor");
            return;
        }
        var scale = transform.localScale;
        var particles = actor.SharedSimulationMesh.Particles;

        var p = gameObject.transform.position;
        var parent = new GameObject(gameObject.name + " Grabbers");
        parent.transform.position.Set(p.x, p.y, p.z);
        int count = 0;
        foreach(var par in particles)
        {
            if (IsCenterParticle(count,particles.Length))
                continue;
            var currpos = transform.position;
            var loc = (par.Position * (new float3(scale.x, scale.y, scale.z))) + (new float3(currpos.x, currpos.y, currpos.z));
            Grabbers.Add(Instantiate(GrabberObject, new Vector3(loc[0],loc[1],loc[2]), new Quaternion(), parent.transform));
            count++;
        }
        IsInitialized = true;
    }


    //Is current particle center? (0, 0, 0) (WIP)
    private bool IsCenterParticle(int index, int total)
    {
        /*
        var x = (double) p.Position.x;
        var y = (double) p.Position.y;
        var z = (double) p.Position.z;
        if (x == 0 && x == y && y == z)
            return true;
        */

        if (index == total - 1)
            return true;
        return false;
    }
}
