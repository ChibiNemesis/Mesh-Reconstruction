using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ParticleGrab : MonoBehaviour
{
    [SerializeField]
    SimpleParticleGrabber grabber;

    private Vector3 InitialPos;

    void Start()
    {
        InitialPos = transform.position;
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

    public void ReleaseAny()
    {
        if (grabber.PhysicsWorld != null)
        {
            grabber.Release();
        }
    }

    public void PrintPos()
    {
        Debug.Log("Difference: " + GetPositionDifference());
    }

    //Used to get each particle's difference from the initial position
    public Vector3 GetPositionDifference()
    {
        return transform.position - InitialPos;
    }
}
