using MAGES.MeshDeformations;
using System.Collections.Generic;
using Unity.Mathematics;
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

    //This should be a copy of the original Sim Mesh, not the same
    [SerializeField]
    public SimulationMesh MeshToSave;

    [SerializeField]
    public GameObject FBXToSave;

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
        //if (EnableKinematic)
        //    PrintParticlePosition("Init");
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
                //if (EnableKinematic)
                //    PrintParticlePosition("Final");
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
            //if(EnableKinematic)
            //    PrintParticlePosition("Final");
        }
    }

    //FIX THIS NOW
    private void ChangeParticlePosition()
    {
        var actor = GetComponent<SoftbodyActor>();
        Particle[] originalParticles = actor.SharedSimulationMesh.Particles;
        Particle[] particles = MeshToSave.Particles;

        for (int p = 0; p < particles.Length; p++)
        {
            // Convert particle position to world space
            Vector3 initialWorldPos = transform.TransformPoint((Vector3)particles[p].Position);

            for (int g = 0; g < Grabbers.Count; g++)
            {
                // Ensure grabber position is in the same coordinate space
                Vector3 grabberWorldInitPos = Grabbers[g].GetComponent<ParticleGrab>().GetInitialPosition();

                // Compare with tolerance
                if (Vector3.Distance(initialWorldPos, grabberWorldInitPos) < 0.0001f)
                {
                    Vector3 difference = Grabbers[g].GetComponent<ParticleGrab>().GetPositionDifference();

                    // Convert difference back to object space and apply
                    Vector3 localDiff = transform.InverseTransformVector(difference);
                    particles[p].Position = originalParticles[p].Position + (float3)localDiff;
                    break; // Found the matching grabber, no need to continue
                }
            }
        }
    }
    /*private void ChangeParticlePosition()
    {
        var OriginalParticles = GetComponent<SoftbodyActor>().SharedSimulationMesh.Particles;
        var Particles = MeshToSave.Particles;
        var position = transform.position;
        var scale = transform.localScale;
        
        for(var p = 0; p < Particles.Length; p++)
        {
            var InitialPos = (new Vector3(Particles[p].Position.x, Particles[p].Position.y, Particles[p].Position.z)) + position;
            for(var g = 0; g < Grabbers.Count; g++)
            {
                var GInitPos = Grabbers[g].GetComponent<ParticleGrab>().GetInitialPosition();
                if(Vector3.Distance(InitialPos, GInitPos) < 0.0001f)
                {
                    var difference = Grabbers[g].GetComponent<ParticleGrab>().GetPositionDifference();
                    Particles[p].Position = OriginalParticles[p].Position + (new float3(difference.x / scale.x, difference.y / scale.y, difference.z / scale.z));
                    break;
                }
            }
        }
    }*/

    public void SaveNewModel()
    {
        if (FBXToSave == null)
            return;

        //this is a copy
        var mesh = FBXToSave.GetComponent<MeshFilter>().sharedMesh;
        var CopyScale = FBXToSave.transform.localScale;
        Vector3[] Vertices = mesh.vertices;

        for(var v = 0; v < Vertices.Length; v++)
        {
            foreach(var grab in Grabbers)
            {
                var init = grab.GetComponent<ParticleGrab>().GetInitialPosition();
                var scale = transform.localScale;
                var n = init - transform.position;
                var final = grab.transform.position - transform.position;

                var Initial = new Vector3(RoundDigit(n.x / scale.x), RoundDigit(n.y / scale.y), RoundDigit(n.z / scale.z));
                var Final = new Vector3(RoundDigit(final.x / scale.x), RoundDigit(final.y / scale.y), RoundDigit(final.z / scale.z));
                if (Vertices[v] == Initial)
                {
                    Vertices[v].x = final.x/(scale.x);
                    Vertices[v].y = final.y/(scale.y);
                    Vertices[v].z = final.z/(scale.z);
                }
            }
        }
        mesh.vertices = Vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    public void SaveNewModelV2()
    {
        if (FBXToSave == null)
            return;

        //this is a copy
        var mesh = FBXToSave.GetComponent<MeshFilter>().sharedMesh;
        //var CopyScale = FBXToSave.transform.localScale;
        Vector3[] Vertices = mesh.vertices;

        for(int g = 0; g < Grabbers.Count; g++)
        {
            var init = Grabbers[g].GetComponent<ParticleGrab>().GetInitialPosition();
            var scale = transform.localScale;
            var n = init - transform.position;
            var final = Grabbers[g].transform.position - transform.position;

            List<int> VertexPositions = Grabbers[g].GetComponent<ParticleGrab>().GetMeshVertices();
            foreach(var index in VertexPositions)
            {
                var Initial = Vertices[index];
                var Final = new Vector3(RoundDigit(final.x / scale.x), RoundDigit(final.y / scale.y), RoundDigit(final.z / scale.z));

                Vertices[index].x = final.x / (scale.x);
                Vertices[index].y = final.y / (scale.y);
                Vertices[index].z = final.z / (scale.z);
            }
        }

        /*
        for (var v = 0; v < Vertices.Length; v++)
        {
            foreach (var grab in Grabbers)
            {
                var init = grab.GetComponent<ParticleGrab>().GetInitialPosition();
                var scale = transform.localScale;
                var n = init - transform.position;
                var final = grab.transform.position - transform.position;

                var Initial = new Vector3(RoundDigit(n.x / scale.x), RoundDigit(n.y / scale.y), RoundDigit(n.z / scale.z));
                var Final = new Vector3(RoundDigit(final.x / scale.x), RoundDigit(final.y / scale.y), RoundDigit(final.z / scale.z));
                if (Vertices[v] == Initial)
                {
                    Vertices[v].x = final.x / (scale.x);
                    Vertices[v].y = final.y / (scale.y);
                    Vertices[v].z = final.z / (scale.z);
                }
            }
        }*/
        mesh.vertices = Vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    //Function that rounds numbers
    //Ex: 0.00249999999 -> 0.0025
    //Ex: 0.002500000001 -> 0.0025
    private float RoundDigit(float num, int tries = 10)
    {
        float d = 0.000000001f;
        for(int t = 1; t <= tries; t++)
        {
            var numPos = num + (d * t);
            var numNeg = num - (d * t);
            if(numPos.ToString().Length < num.ToString().Length)
            {
                return (float) numPos;
            }
            if (numNeg.ToString().Length < num.ToString().Length)
            {
                return (float) numNeg;
            }
        }
        //if no rounding is needed
        return (float) num;
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
            ChangeParticlePosition(); //modify copy simulation mesh
            SaveNewModel(); //modify copy mesh
            EnableKinematic = false;
        }
    }
}
