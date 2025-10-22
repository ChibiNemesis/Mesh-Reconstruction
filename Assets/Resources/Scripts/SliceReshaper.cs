using MAGES.MeshDeformations;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(BoundsSlicer))]
[RequireComponent(typeof(GrabInitializer))]
public class SliceReshaper : MonoBehaviour
{

    [SerializeField]
    public BoundsSlicer Slicer;

    [SerializeField]
    public GrabInitializer Generator;

    [SerializeField]
    public List<BoundsPoints> Slices;

    //Contains all grabbers for the whole mesh
    [SerializeField]
    public List<GameObject> Grabbers;

    //Contains each slice's grabbers seperately
    [SerializeField]
    public List<SliceData> SliceGrabbers;

    [SerializeField]
    public bool InterpolatedDeformation = true;

    [SerializeField]
    public bool EnableKinematic = false;

    //Used for Interpolated deformation only
    [SerializeField]
    private int TotalIterations = 1000;

    //false only if we want to insert data manually 
    public bool Initialize = false;

    private bool DeformLock = false;
    private bool IsFinished = false;

    private int CurrentIteration = 0;

    private const float DeformIteration = 0.001f;

    //This should be a copy of the original
    [SerializeField]
    public SimulationMesh MeshToSave;

    void Start()
    {
        if (!Slicer)
        {
            Slicer = GetComponent<BoundsSlicer>();
        }
        Slicer.CreateSeperateBoxes();
        Slices = Slicer.GetSlices();

        if (Initialize)
        {
            Grabbers = new List<GameObject>();
            SliceGrabbers = new List<SliceData>();
            Generator.GenerateGrabbers();
            InitializeSliceData();

            var si = GetComponent<SliceInitializer>();
            if (si != null)
            {
                si.InitializeSlices();
            }
        }
    }

    private void InitializeSliceData()
    {
        Grabbers = Generator.Grabbers;
        foreach(var s in Slices)
        {
            float x, y, z, xs, ys, zs;
            var ADDITION = 0.0001f;
            //Use this to transform all slices to new area
            x = transform.position.x;
            y = transform.position.y;
            z = transform.position.z;

            //Also, use this for scaling in case scale is not 1
            xs = transform.localScale.x;
            ys = transform.localScale.y;
            zs = transform.localScale.z;


            var Nmin = new Vector3((s.Min.x * xs) + x - ADDITION, (s.Min.y * ys) + y - ADDITION, (s.Min.z * zs) + z - ADDITION);
            var Nmax = new Vector3((s.Max.x * xs) + x + ADDITION, (s.Max.y * ys) + y + ADDITION, (s.Max.z * zs) + z + ADDITION);

            SliceData data = new SliceData(Nmin, Nmax);
            Bounds b = new Bounds();
            b.SetMinMax(Nmin, Nmax);
            foreach(var g in Grabbers)
            {
                g.GetComponent<ParticleGrab>().GrabAny();
                if (b.Contains(g.transform.position))
                {
                    data.Grabbers.Add(g);
                }
            }
            SliceGrabbers.Add(data);
        }
        if (EnableKinematic)
            PrintParticlePosition("Init");
    }

    public void DeformSlices()
    {
        foreach(var s in SliceGrabbers)
        {
            var total = s.Grabbers.Count;
            if (InterpolatedDeformation)
            {
                MoveParticlesPeriodically(s);
            }
            else
            {
                //instant mode
                for (var g = 0; g < total; g++)
                {
                    var Current = s.Grabbers[g].transform.position;
                    var Final = s.Destinations[g];
                    var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                    s.Grabbers[g].transform.Translate(movement, Space.World);
                    IsFinished = true;
                }
                if (EnableKinematic)
                    PrintParticlePosition("Final");
            }
        }
    }

    private void MoveParticlesPeriodically(SliceData s)
    {
        var total = s.Grabbers.Count;
        CurrentIteration++;
        for (int g = 0; g < total; g++)
        {
            var Current = s.Grabbers[g].transform.position;
            var Final = s.Destinations[g];
            if (CurrentIteration == TotalIterations - 1)
            {
                IsFinished = true;
            }
            var next = Vector3.MoveTowards(Current, Final, DeformIteration);
            s.Grabbers[g].transform.position = next;
        }
        if (CurrentIteration == TotalIterations)
        {
            IsFinished = true;
            if(EnableKinematic)
                PrintParticlePosition("Final");
        }
    }

    private void ChangeParticlePosition()
    {
        //Use this to reverse particle position back to 0, 0, 0
        var OriginalParticles = GetComponent<SoftbodyActor>().SharedSimulationMesh.Particles;
        var Particles = MeshToSave.Particles;
        var scale = transform.localScale;
        for (var p = 0; p < Particles.Length; p++)
        {
            var difference = Grabbers[p].GetComponent<ParticleGrab>().GetPositionDifference();
            Particles[p].Position = OriginalParticles[p].Position + (new float3(difference.x/scale.x, difference.y/scale.y, difference.z/scale.z));
        }
    }

    private void ChangeKinematicParticles()
    {
        var Particles = GetComponent<SoftbodyActor>().SimulationMesh.Particles;
        for (var p = 0; p < Particles.Length; p++)
        {
            Particles[p].Kinematic = false;
        }
    }

    private void ReleaseAllGrabbers()
    {
        foreach(var grab in Grabbers)
        {
            //grab.GetComponent<ParticleGrab>().PrintPos();
            grab.GetComponent<ParticleGrab>().ReleaseAny();
        }
    }

    private void PrintParticlePosition(string id)
    {
        var Particles = GetComponent<SoftbodyActor>().SharedSimulationMesh.Particles;
        for (var p = 0; p < Particles.Length; p++)
        {
            if (Particles[p].Kinematic)
            {
                print("Pos( "+id+" ): " + Particles[p].Position);
            }
        }
        var mesh = GetComponent<MeshFilter>().sharedMesh.vertices;
        foreach(var ver in mesh)
        {
            Debug.Log("V: " + ver);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) && !DeformLock)
        {
            DeformLock = true;
            DeformSlices();
        }

        if (!IsFinished && DeformLock && InterpolatedDeformation)
        {
            foreach (var s in SliceGrabbers)
            {
                MoveParticlesPeriodically(s);
            }
        }

        if(Input.GetKeyDown(KeyCode.R) && EnableKinematic && IsFinished)
        {
            Debug.Log("Enable Particles");
            ChangeParticlePosition();
            //ChangeKinematicParticles();
            ReleaseAllGrabbers();
            EnableKinematic = false;
        }
    }
}
