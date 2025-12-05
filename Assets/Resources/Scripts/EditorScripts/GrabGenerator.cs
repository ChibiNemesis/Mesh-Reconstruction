using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;
using MAGES.MeshDeformations;

public class ParticleGrabGenerator : EditorWindow
{
    public Object Object;
    public Object Grabber;
    public Object PhysicsWorld;
    [MenuItem("Window/Particle Grab Generator")]
    public static void ShowWindow()
    {
        GetWindow<ParticleGrabGenerator>("Grab Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Object From Scene");
        Object = EditorGUILayout.ObjectField(Object, typeof(GameObject), true);
        EditorGUILayout.LabelField("Grabber Object");
        Grabber = EditorGUILayout.ObjectField(Grabber, typeof(GameObject), true);
        EditorGUILayout.LabelField("Physics World");
        PhysicsWorld = EditorGUILayout.ObjectField(PhysicsWorld, typeof(PhysicsWorld), true);
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Generate Grabbers"))
        {
            FunctionToRunV2();
        }
    }

    //Generates Particles Based on Mesh Vertices
    private void FunctionToRunV2()
    {
        if (Object != null && Grabber != null)
        {
            Debug.Log("Generating Grabbers for " + Object.name + " Using Mesh's Vertices");

            var copy = (GameObject)Object;
            Vector3[] Vertices = copy.GetComponent<MeshFilter>().sharedMesh.vertices;
            var Position = copy.transform.position;
            var Scale = copy.transform.localScale;
            var parent = new GameObject(copy.name + " Grabbers");

            //Store positions where there is already one grabber
            List<Vector3> InitializedPositions = new List<Vector3>();
            List<GameObject> InitializedGrabbers = new List<GameObject>();
            int count = 0;
            foreach (var ver in Vertices)
            {
                if (InitializedPositions.Contains(ver))
                {
                    int index = InitializedPositions.IndexOf(ver);
                    InitializedGrabbers[index].GetComponent<ParticleGrab>().AddVertex(count);
                    count++;
                    continue;
                }
                var loc = (ver * (new float3(Scale.x, Scale.y, Scale.z))) + (new float3(Position.x, Position.y, Position.z));
                var grab = (GameObject)Instantiate(Grabber, new Vector3(loc[0], loc[1], loc[2]), new Quaternion(), parent.transform);
                grab.GetComponent<SimpleParticleGrabber>().PhysicsWorld = (PhysicsWorld)PhysicsWorld;
                var pg = grab.GetComponent<ParticleGrab>();
                if (pg == null)
                {
                    Debug.LogWarning("Grabber Object: " + Grabber.name + " Does not Contain a ParticleGrab Component");
                }
                else
                {
                    pg.AddVertex(count);
                }

                InitializedPositions.Add(ver);
                InitializedGrabbers.Add(grab);


                count++;
            }
            var actor = copy.GetComponent<SoftbodyActor>();
            SetAllParticlesKinematic(actor);
        }
    }

    private void SetAllParticlesKinematic(SoftbodyActor actor, bool val = true)
    {
        Particle[] Particles = actor.SharedSimulationMesh.Particles;
        for (var p=0;p<Particles.Length;p++)
        {
            Particles[p].Kinematic = val;
        }
    }

    //Is current particle center? Suppose Non Kinematic Particles are centered
    private bool IsCenterParticle(int index, BasePhysicsActor actor)
    {

        return !actor.SharedSimulationMesh.Particles[index].Kinematic;
    }
}
