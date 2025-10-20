using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;


[ExecuteInEditMode]
public class GrabInitializer : MonoBehaviour
{
    [SerializeField]
    public BasePhysicsActor actor;

    public List<GameObject> Grabbers { set; get; }

    //private bool IsInitialized = false;

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
        //var parent = new GameObject(gameObject.name + " Grabbers");
        var par1 = GameObject.Find(gameObject.name + " Grabbers");
        Grabbers = new List<GameObject>();
        if (par1 != null) 
        {
            foreach(Transform child in par1.transform)
            {
                Grabbers.Add(child.gameObject);
            }
            return;
        }
    }
}
