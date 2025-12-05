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
    private bool Wireframe = false;

    private int CurrentIteration = 0;

    private const float DeformIteration = 0.001f;

    private List<Material> Materials;

    //Each vertex known which vertices are connected to it theough the mesh
    public Dictionary<int, HashSet<int>> VertexAdjacency;

    //Each Grabber knows which ParticleGrabbers are connected to it through the mesh
    public Dictionary<ParticleGrab, HashSet<ParticleGrab>> GrabberAdjacency;

    //This should be a copy of the original Sim Mesh, not the same
    [SerializeField]
    public SimulationMesh MeshToSave;

    [SerializeField]
    MeshFilter MeshToCompare;

    [SerializeField]
    public bool Statistics = false;

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
        //Save materials in case you need to 
        Materials = new List<Material>();
        var m = GetComponent<MeshRenderer>().materials;
        for(var a = 0; a < m.Length; a++)
        {
            Materials.Add(m[a]);
        }
    }

    private void InitializeSliceData()
    {
        BuildAdjacency(GetComponent<MeshFilter>().mesh);
        List<ParticleGrab> AllGrabbers = new List<ParticleGrab>();
        foreach(var g in Grabbers)
        {
            AllGrabbers.Add(g.GetComponent<ParticleGrab>());
        }

        BuildGrabberAdjacency(AllGrabbers);

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

    /// <summary>
    /// Creates a Dictionary where each item represents a vertex and each list represents all adjacent vertices
    /// </summary>
    /// <param name="mesh">Mesh component used for the creation of the dictionary</param>
    void BuildAdjacency(Mesh mesh)
    {
        VertexAdjacency = new Dictionary<int, HashSet<int>>();

        int[] tris = mesh.triangles;

        for (int i = 0; i < mesh.vertexCount; i++)
            VertexAdjacency[i] = new HashSet<int>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i];
            int b = tris[i + 1];
            int c = tris[i + 2];

            VertexAdjacency[a].Add(b);
            VertexAdjacency[a].Add(c);

            VertexAdjacency[b].Add(a);
            VertexAdjacency[b].Add(c);

            VertexAdjacency[c].Add(a);
            VertexAdjacency[c].Add(b);
        }
    }

    /// <summary>
    /// Creates a Dictionary, where each item represents a Grabber and each list represents all adjacent grabbers
    /// </summary>
    /// <param name="allGrabbers"></param>
    /// <param name="mesh"></param>
    void BuildGrabberAdjacency(List<ParticleGrab> allGrabbers) //Mesh mesh
    {
        GrabberAdjacency = new Dictionary<ParticleGrab, HashSet<ParticleGrab>>();

        // Build map from vertex index grabber
        Dictionary<int, ParticleGrab> vertexOwner = new Dictionary<int, ParticleGrab>();
        foreach (var g in allGrabbers)
        {
            foreach (var v in g.GetMeshVertices())
                vertexOwner[v] = g;
        }

        foreach (var g in allGrabbers)
            GrabberAdjacency[g] = new HashSet<ParticleGrab>();

        // For each vertex in each grabber, find neighbors
        foreach (var g in allGrabbers)
        {
            foreach (int v in g.GetMeshVertices())
            {
                foreach (int adjV in VertexAdjacency[v])
                {
                    if (vertexOwner.TryGetValue(adjV, out var adjG))
                    {
                        if (adjG != g)
                            GrabberAdjacency[g].Add(adjG);
                    }
                }
            }
        }
    }

    public void DeformSlices()
    {
        if (DeformLock)
            return;

        DeformLock = true;
        CurrentIteration = 0;  
        IsFinished = false;
        int count = 0;
        foreach (var s in SliceGrabbers)
        {
            //var total = s.Grabbers.Count;
            if (InterpolatedDeformation)
            {
                MoveParticlesPeriodically(s, count);
            }
            else
            {
                //instant mode
                //Handle Outer Grabbers first (or grabbers in general from other slice initializers)
                for (var g = 0; g < s.Grabbers.Count; g++)
                {
                    var Current = s.Grabbers[g].transform.position;
                    var Final = s.Destinations[g]; //Destinations contains Outer grabbers only
                    var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                    s.Grabbers[g].transform.Translate(movement, Space.World);
                    IsFinished = true;
                }

                //Edge slices
                if (s.IsEdgeSlice)
                {
                    //Then Handle inner Grabbers
                    if(s.InnerGrabbers!= null)
                    {
                        Debug.Assert(s.InnerGrabbers.Count == s.InnerDestinations.Count);
                        for(var i = 0; i < s.InnerGrabbers.Count; i++)
                        {
                            var Current = s.InnerGrabbers[i].transform.position;
                            var Final = s.InnerDestinations[i];
                            var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                            s.InnerGrabbers[i].transform.Translate(movement, Space.World);
                            IsFinished = true;
                        }
                    }
                }
            }
            count++;
        }
    }

    private void MoveParticlesPeriodically(SliceData s, int Index)
    {
        Debug.Assert(s.Grabbers.Count == s.Destinations.Count);
        for (int g = 0; g < s.Grabbers.Count; g++)
        {
            var current = s.Grabbers[g].transform.position;
            var final = s.Destinations[g];

            var next = Vector3.MoveTowards(current, final, DeformIteration);
            s.Grabbers[g].transform.position = next;
        }
        //First and last slices
        if (s.IsEdgeSlice) // Index == 0 || Index == SliceGrabbers.Count - 1
        {
            //Inner Grabbers
            if(s.InnerGrabbers != null)
            {
                Debug.Assert(s.InnerGrabbers.Count == s.InnerDestinations.Count);
                for (int i = 0; i < s.InnerGrabbers.Count; i++)
                {
                    var current = s.InnerGrabbers[i].transform.position;
                    var final = s.InnerDestinations[i];

                    var next = Vector3.MoveTowards(current, final, DeformIteration);
                    s.InnerGrabbers[i].transform.position = next;
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

    // quantize a Vector3 to an integer key for stable float comparisons
    private Vector3Int QuantizePosition(Vector3 pos, float precision = 0.001f)
    {
        return new Vector3Int(
            Mathf.RoundToInt(pos.x / precision),
            Mathf.RoundToInt(pos.y / precision),
            Mathf.RoundToInt(pos.z / precision)
        );
    }

    private void PrintStatistics()
    {
        if (MeshToCompare == null)
        {
            Debug.LogWarning("Please, Add a meshfilter component for comparison");
            return;
        }
        if (IsFinished == false)
        {
            Debug.LogWarning("Please, deform Particles before computing statistics");
            return;
        }

        //Compute Local coordinates for each vertex inside vertex table
        Mesh copy = Instantiate(GetComponent<MeshFilter>().sharedMesh);
        foreach(var gr in Grabbers)
        {
            //Transform grabbers from world to local space based on this object's transform
            Vector3 cp = gr.transform.position;
            var GrabberIndices = gr.GetComponent<ParticleGrab>().GetMeshVertices();
            foreach(var ind in GrabberIndices)
            {
                copy.vertices[ind] = transform.InverseTransformPoint(cp);
            }
        }

        Mesh OriginalNormalized = MeshComparison.NormalizeMesh(MeshToCompare.sharedMesh, transform);
        Mesh DeformedNormalized = MeshComparison.NormalizeMesh(copy, transform);

        Mesh OrigN = MeshComparison.ScaleMeshToFitDistance(OriginalNormalized);
        Mesh DefN = MeshComparison.ScaleMeshToFitDistance(DeformedNormalized);
        Debug.Log("Bounds of original scaled: " + OrigN.bounds.size);
        Debug.Log("Bounds of Deformed scaled: " + DefN.bounds.size);

        List<Vector3> OriginalSamples = MeshComparison.SampleMeshSurface(MeshComparison.ScaleMeshToFitDistance(OriginalNormalized), 1000);
        List<Vector3> DeformedSamples = MeshComparison.SampleMeshSurface(MeshComparison.ScaleMeshToFitDistance(DeformedNormalized), 1000);

        List<Vector3> OriginalNormalSamples = MeshComparison.SampleMeshNormals(OriginalNormalized, OriginalSamples);
        List<Vector3> DeformedNormalSamples = MeshComparison.SampleMeshNormals(DeformedNormalized, DeformedSamples);

        float chamfer = MeshComparison.ComputeChamferDistance(OriginalSamples, DeformedSamples);
        float hausdorff = MeshComparison.ComputeHausdorffDistance(OriginalSamples, DeformedSamples);
        float normals = MeshComparison.ComputeNormalSimilarity(OriginalSamples, DeformedSamples, OriginalNormalSamples, DeformedNormalSamples);
        float WeightedSim = MeshComparison.ComputeMetricsDistanceAverage(chamfer, hausdorff, normals);
        Debug.Log("Mesh Comparison for " + gameObject.name);
        Debug.Log("Chamfer Distance: " + chamfer);
        Debug.Log("Hausdorff Distance: " + hausdorff);
        Debug.Log("Normal Similarity: "+ normals);
        Debug.Log("Weighted Similarity: " + WeightedSim);
    }
    public void SetInterpolation(bool val)
    {
        InterpolatedDeformation = val;
    }

    public Dictionary<int, HashSet<int>> GetAdjacencyDictionary()
    {
        return VertexAdjacency;
    }

    public bool GetInterpolation()
    {
        return InterpolatedDeformation;
    }

    public bool GetIsFinished() 
    { 
        return IsFinished; 
    }

    public void SetWireframe(bool wf, Material WireMat)
    {
        Wireframe = wf;
        OnWireFrameChange(WireMat);
    }

    private void OnWireFrameChange(Material mat)
    {
        Material[] mats = new Material[GetComponent<MeshRenderer>().materials.Length];
        for (int m = 0; m < GetComponent<MeshRenderer>().materials.Length; m++)
        {
            if (Wireframe)
            {
                mats[m] = new Material(mat);
            }
            else
            {
                mats[m] = new Material(Materials[m]);
            }
        }
        GetComponent<MeshRenderer>().materials = mats;
    }

    public bool GetWireFrame()
    {
        return Wireframe;
    }

    public bool GetLock()
    {
        return DeformLock;
    }

    void Update()
    {
        if (!DeformLock || IsFinished)
        {
            if (Statistics)
            {
                Statistics = false;
                PrintStatistics();
            }
            return;
        }

        CurrentIteration++;

        for(int s = 0; s < SliceGrabbers.Count; s++)
        {
            MoveParticlesPeriodically(SliceGrabbers[s], s);
        }

        // Check completion
        if (CurrentIteration >= TotalIterations)
        {
            IsFinished = true;
            DeformLock = false;
        }
    }

}
