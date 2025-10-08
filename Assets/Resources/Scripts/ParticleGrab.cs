using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleGrab : MonoBehaviour
{
    [SerializeField]
    SimpleParticleGrabber grabber;

    // The moment a grabber is spawned on a particle, it instantly grabs it
    void Start()
    {
        if (grabber.PhysicsWorld == null)
        {
            var world = GameObject.FindGameObjectWithTag("PhysWorld");
            grabber.PhysicsWorld = world.GetComponent<PhysicsWorld>();
        }
        //grabber.Grab();
        if(grabber.PhysicsWorld != null)
        {
            grabber.Grab();
        }
    }

    /*public void SetWorld(PhysicsWorld _world)
    {
        grabber.PhysicsWorld = _world;
    }*/

    public void GrabAny()
    {
        grabber.Grab();
    }
}
