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
    public MeshReconstructor Reconstructor;

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
        if(Reconstructor != null)
        {
            Reconstructor.grabbers = new List<ParticleGrab>();
            foreach(var gr in Grabbers)
            {
                Reconstructor.grabbers.Add(gr.GetComponent<ParticleGrab>());
            }
        }
        else
        {
            Debug.LogWarning("Please, Add Reconstructor if you want to save the new 3d model");
        }
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
                    Reconstructor.SetFinished();
                }
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
                if(Reconstructor!=null)
                    Reconstructor.SetFinished();
            }
            var next = Vector3.MoveTowards(Current, Final, DeformIteration);
            s.Grabbers[g].transform.position = next;
        }
        if (CurrentIteration == TotalIterations)
        {
            IsFinished = true;
            if (Reconstructor != null)
                Reconstructor.SetFinished();
        }
    }

    private void ChangeParticlePositionOld()
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

    private void ChangeParticlePosition()
    {
        var actor = GetComponent<SoftbodyActor>();
        Particle[] originalParticles = actor.SharedSimulationMesh.Particles;
        Particle[] particles = MeshToSave.Particles;

        // --- STEP 1: Build a lookup table for particles by (approximate) world position ---
        // Because we can’t directly hash floating-point positions, we round or quantize them.
        Dictionary<Vector3Int, int> particleLookup = new Dictionary<Vector3Int, int>();

        for (int p = 0; p < particles.Length; p++)
        {
            Vector3 worldPos = transform.TransformPoint((Vector3)particles[p].Position);
            Vector3Int quantized = QuantizePosition(worldPos);
            if (!particleLookup.ContainsKey(quantized))
                particleLookup.Add(quantized, p);
        }

        // --- STEP 2: Apply grabber deltas efficiently ---
        foreach (var grabber in Grabbers)
        {
            Vector3 grabInitPos = grabber.GetComponent<ParticleGrab>().GetInitialPosition();
            Vector3Int quantizedGrabPos = QuantizePosition(grabInitPos);

            if (particleLookup.TryGetValue(quantizedGrabPos, out int pIndex))
            {
                Vector3 difference = grabber.GetComponent<ParticleGrab>().GetPositionDifference();

                // Convert to local space delta for the simulation mesh
                Vector3 localDiff = transform.InverseTransformVector(difference);
                particles[pIndex].Position = originalParticles[pIndex].Position + (float3)localDiff;
            }
            else
            {
                // Optional debug: report any unmatched grabber
                Debug.LogWarning($"Grabber at {grabInitPos} did not match any particle.");
            }
        }
    }

    // --- Utility: quantize a Vector3 to an integer key for stable float comparisons ---
    private Vector3Int QuantizePosition(Vector3 pos, float precision = 0.001f)
    {
        return new Vector3Int(
            Mathf.RoundToInt(pos.x / precision),
            Mathf.RoundToInt(pos.y / precision),
            Mathf.RoundToInt(pos.z / precision)
        );
    }

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

        /*
        if(Input.GetKeyDown(KeyCode.R) && EnableKinematic && IsFinished)
        {
            ChangeParticlePosition(); //modify copy simulation mesh
            //SaveNewModel(); //modify copy mesh
            EnableKinematic = false;
        }*/
    }
}
