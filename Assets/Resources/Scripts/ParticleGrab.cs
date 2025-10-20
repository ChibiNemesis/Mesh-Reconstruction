using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ParticleGrab : MonoBehaviour
{
    [SerializeField]
    SimpleParticleGrabber grabber;

    void Start()
    {
        if (grabber.PhysicsWorld == null)
        {
            var world = GameObject.FindGameObjectWithTag("PhysWorld");
            grabber.PhysicsWorld = world.GetComponent<PhysicsWorld>();
        }
        GrabAny();
    }

    public void GrabAny()
    {
        if(grabber.PhysicsWorld != null)
        {
            grabber.Grab();
        }
    }
}
