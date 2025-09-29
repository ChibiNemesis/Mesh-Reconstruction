using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MAGES.MeshDeformations;

public class ParticlesRellocationTest : MonoBehaviour
{
    [SerializeField]
    SoftbodyActor actor;
    
    void Start()
    {
        if(actor==null)
            actor = GetComponent<SoftbodyActor>();
        RellocateParticles();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void RellocateParticles()
    {
        int TotalParticles = actor.SharedSimulationMesh.Particles.Length;
        Particle[] par = actor.SharedSimulationMesh.Particles;
        Debug.Log(TotalParticles);
        float reshape = 1.05f;
        for (int i = 0; i < TotalParticles; i++)
        {
            if (i == 90)
            {
                reshape = 1f;
            }
            //par[i].Position = par[i].Position;
            par[i].Position = new Vector3(par[i].Position.x*reshape, par[i].Position.y*reshape, par[i].Position.y*reshape);
            //Debug.Log(par[i].Position);
        }
    }
}
