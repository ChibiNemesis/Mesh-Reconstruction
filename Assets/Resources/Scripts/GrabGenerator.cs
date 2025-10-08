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

        var world = GetComponent<PhysicsWorld>();
        foreach(var par in particles)
        {
            if (IsCenterParticle(count))
                continue;
            var currpos = transform.position;
            var loc = (par.Position * (new float3(scale.x, scale.y, scale.z))) + (new float3(currpos.x, currpos.y, currpos.z));
            var grab = Instantiate(GrabberObject, new Vector3(loc[0], loc[1], loc[2]), new Quaternion(), parent.transform);
            //grab.GetComponent<ParticleGrab>().SetWorld(world);
            Grabbers.Add(grab);
            count++;
        }
        IsInitialized = true;
    }


    //Is current particle center? Suppose Non Kinematic Particles are centered
    private bool IsCenterParticle(int index)
    {

        return !actor.SharedSimulationMesh.Particles[index].Kinematic;
    }
}
