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
            FunctionToRun();
        }
    }

    private void FunctionToRun()
    {
        if(Object != null && Grabber != null)
        {
            Debug.Log(Object.name);
            Debug.Log(Grabber.name);
            var copy = (GameObject)Object;
            var particles = copy.GetComponent<GrabGenerator>().actor.SharedSimulationMesh.Particles;
            var scale = copy.transform.localScale;
            var p = copy.transform.position;

            var parent = new GameObject(copy.name + " Grabbers");
            parent.transform.position.Set(p.x, p.y, p.z);
            int count = 0;
            //copy.GetComponent<GrabGenerator>().Grabbers = new List<GameObject>();
            //var Grabbers = copy.GetComponent<GrabGenerator>().Grabbers;

            foreach (var par in particles)
            {
                if (IsCenterParticle(count, copy.GetComponent<GrabGenerator>().actor))
                    continue;
                //var currpos = copy.transform.position;
                var currpos = copy.transform.localPosition;
                var loc = (par.Position * (new float3(scale.x, scale.y, scale.z))) + (new float3(currpos.x, currpos.y, currpos.z));
                var grab =(GameObject) Instantiate(Grabber, new Vector3(loc[0], loc[1], loc[2]), new Quaternion(), parent.transform);
                grab.GetComponent<SimpleParticleGrabber>().PhysicsWorld = (PhysicsWorld) PhysicsWorld;
                //Grabbers.Add(grab);
                count++;
            }
        }
        else
        {
            Debug.LogWarning("Please initialize Object from scene and grabber");
        }
    }

    //Is current particle center? Suppose Non Kinematic Particles are centered
    private bool IsCenterParticle(int index, BasePhysicsActor actor)
    {

        return !actor.SharedSimulationMesh.Particles[index].Kinematic;
    }
}
