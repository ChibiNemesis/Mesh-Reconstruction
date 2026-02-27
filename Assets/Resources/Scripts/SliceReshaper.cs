using MAGES.MeshDeformations;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(BoundsSlicer))]
[RequireComponent(typeof(GrabInitializer))]
public class SliceReshaper : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    public BoundsSlicer Slicer;
    [SerializeField]
    public GrabInitializer Generator;
    [SerializeField]
    public MeshReconstructorV2 Reconstructor;

    [Header("Particle Grabber Data")]
    [SerializeField] public List<GameObject> Grabbers; //Contains all grabbers for the whole mesh
    [SerializeField] public List<SliceData> SliceGrabbers; //Contains each slice's grabbers seperately

    [Header("Topological Settings")]
    [SerializeField] bool UseTubeTopology = true; // Enable this for your new "Thick Slice" method

    [Header("Mesh Statistics")]
    [SerializeField] public bool Statistics = false;
    [SerializeField] MeshFilter MeshToCompare;

    [Header("Flags")]
    [SerializeField] public bool Initialize = false; //false only if we want to insert data manually 
    [SerializeField] public bool InterpolatedDeformation = true;
    [SerializeField] private int TotalIterations = 1000; //Used for Interpolated deformation only
    [SerializeField] public bool EnableKinematic = false;

    [SerializeField] public List<BoundsPoints> Slices;

    private bool DeformLock = false;
    private bool IsFinished = false;
    private bool Wireframe = false;

    private int CurrentIteration = 0;

    private const float DeformIteration = 0.001f;

    private List<Material> Materials;

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
            InitializeSliceData(); //assign grabbers to slices
            SeparateGrabbers(); // separate inner and outer grabbers for each slice, so that we can handle them differently during deformation
            InitializeDefaultDestinations();

            //Adjusting the Locked Axis
            var la = GetComponent<LockedAxisAdjuster>();
            if (la != null)
            {
                la.AdjustLockedAxis();
            }

            //var si = GetComponent<SliceInitializer>();
            var si = GetComponent<ContourInitializerV2>();
            if (si != null)
            {
                si.InitializeContourData();
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
        if(!InterpolatedDeformation)
            PrintStatistics();
    }

    private void SeparateGrabbers()
    {
        AxisCut axis = SliceGrabbers[0].axis;
        int totalSlices = SliceGrabbers.Count;

        for (int i = 0; i < totalSlices; i++)
        {
            var slice = SliceGrabbers[i];
            slice.OuterGrabbers = new List<GameObject>();
            slice.InnerGrabbers = new List<GameObject>();

            // --- TUBE MODE LOGIC ---
            // If enabled, we treat the "Middle" slices as purely surface rings (Outer Only).
            // We only use Convex Hull + Barycentric mapping for the "Caps" (First and Last slice).
            bool isCap = (i == 0 || i == totalSlices - 1);

            if (UseTubeTopology && !isCap)
            {
                // FORCE OUTER: All grabbers in this slice become Outer Grabbers.
                // This forces the Raycaster to project EVERY point in this slice to the target surface.
                slice.OuterGrabbers.AddRange(slice.Grabbers);

                // Inner list remains empty.
                // NOTE: This assumes these middle slices effectively act as the "skin".
                continue;
            }

            // --- STANDARD CONVEX HULL LOGIC (For Caps or Standard Mode) ---
            if (slice.Grabbers.Count < 4)
            {
                slice.OuterGrabbers.AddRange(slice.Grabbers);
                continue;
            }

            //SeparateCapGrabbers(slice, axis, i == 0);
            bool isTopCap = (i == 0); // i == totalSlices - 1
            SeparateCapByPercentage(slice, axis, isTopCap, 0.4f); // You can adjust the percentage as needed

            /*List<(GameObject obj, Vector2 p)> pts = new();
            foreach (var g in slice.Grabbers)
            {
                Vector3 wp = g.transform.position;
                pts.Add((g, ProjectTo2D(wp, axis)));
            }

            var hull = ComputeConvexHull(pts);
            HashSet<GameObject> hullSet = new(hull);

            foreach (var (obj, p) in pts)
            {
                if (hullSet.Contains(obj)) slice.OuterGrabbers.Add(obj);
                else slice.InnerGrabbers.Add(obj);
            }*/

            // Remove Outer from the main Grabbers list to allow smoothing to work distinctly
            // (Only if you want separate smoothing lists)
            //slice.Grabbers.RemoveAll(item => slice.InnerGrabbers.Contains(item));
        }
    }

    private void SeparateCapGrabbers(SliceData slice, AxisCut axis, bool isTopCap)
    {
        Vector3 centroid = CalculateCentroid(slice.Grabbers);
        float roofAngleThreshold = 30f; // Degrees: Adjust between 30-60 to grab more/less roof.

        foreach (var g in slice.Grabbers)
        {
            // 1. Get direction from centroid to grabber
            Vector3 dir = (g.transform.position - centroid).normalized;

            // 2. Get the UP vector for your locked axis
            Vector3 upVector = axis switch { AxisCut.X => Vector3.right, AxisCut.Z => Vector3.forward, _ => Vector3.up };

            // 3. Calculate angle
            float angle = Vector3.Angle(upVector, dir);

            // 4. Classify
            if (isTopCap)
            {
                // Pointing mostly UP (Roof)
                if (angle <= roofAngleThreshold)
                    slice.InnerGrabbers.Add(g);
                // Pointing mostly SIDEWAYS (Tube)
                else
                    slice.OuterGrabbers.Add(g);
            }
            else // Bottom Cap
            {
                // Pointing mostly DOWN (Floor)
                if (angle >= (180f - roofAngleThreshold))
                    slice.InnerGrabbers.Add(g);
                // Pointing mostly SIDEWAYS (Tube)
                else
                    slice.OuterGrabbers.Add(g);
            }
        }
    }


    private void SeparateCapByPercentage(SliceData slice, AxisCut axis, bool isTopCap, float percentage = 0.2f)
    {
        // 1. Find Min and Max height of this specific slice
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        foreach (var g in slice.Grabbers)
        {
            float val = GetAxisValue(g.transform.position, axis);
            if (val < minVal) minVal = val;
            if (val > maxVal) maxVal = val;
        }

        float sliceHeight = maxVal - minVal;

        // 2. Classify based on height
        foreach (var g in slice.Grabbers)
        {
            float val = GetAxisValue(g.transform.position, axis);

            if (isTopCap)
            {
                // If in the top 20% of the slice -> Roof
                float threshold = maxVal - (sliceHeight * percentage);
                if (val >= threshold) slice.InnerGrabbers.Add(g);
                else slice.OuterGrabbers.Add(g);
            }
            else // Bottom cap
            {
                // If in the bottom 20% of the slice -> Floor
                float threshold = minVal + (sliceHeight * percentage);
                if (val <= threshold) slice.InnerGrabbers.Add(g);
                else slice.OuterGrabbers.Add(g);
            }
        }
    }

    private float GetAxisValue(Vector3 v, AxisCut axis)
    {
        return axis switch { AxisCut.X => v.x, AxisCut.Y => v.y, AxisCut.Z => v.z, _ => v.y };
    }

    private Vector3 CalculateCentroid(List<GameObject> grabbers)
    {
        if (grabbers == null || grabbers.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var g in grabbers)
        {
            sum += g.transform.position;
        }
        return sum / grabbers.Count;
    }

    // Projects 3D point onto 2D plane depending on slicing axis
    private Vector2 ProjectTo2D(Vector3 p, AxisCut axis)
    {
        return axis switch
        {
            AxisCut.Y => new Vector2(p.x, p.z),
            AxisCut.X => new Vector2(p.y, p.z),
            AxisCut.Z => new Vector2(p.x, p.y),
            _ => new Vector2(p.x, p.z)
        };
    }

    private float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    // Graham Scan convex hull
    private List<GameObject> ComputeConvexHull(List<(GameObject obj, Vector2 p)> pts)
    {
        // Sort by x then y
        pts.Sort((a, b) =>
        {
            int c = a.p.x.CompareTo(b.p.x);
            return c != 0 ? c : a.p.y.CompareTo(b.p.y);
        });

        List<(GameObject obj, Vector2 p)> hull = new();

        // Lower hull
        foreach (var pt in pts)
        {
            while (hull.Count >= 2 &&
                 Cross(hull[hull.Count - 2].p, hull[hull.Count - 1].p, pt.p) <= 0)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(pt);
        }

        // Upper hull
        int lowerCount = hull.Count;
        for (int i = pts.Count - 1; i >= 0; i--)
        {
            var pt = pts[i];
            while (hull.Count > lowerCount &&
                  Cross(hull[hull.Count - 2].p, hull[hull.Count - 1].p, pt.p) <= 0)
                hull.RemoveAt(hull.Count - 1);

            hull.Add(pt);
        }

        hull.RemoveAt(hull.Count - 1);

        List<GameObject> result = new();
        foreach (var h in hull) result.Add(h.obj);
        return result;
    }

    //Called if no Slice Initializer exists on this object
    //Initialize each Grabber with its initial position
    private void InitializeDefaultDestinations()
    {
        for(int s = 0; s < SliceGrabbers.Count; s++)
        {

            // Initialize InnerDestinations with initial InnerGrabbers positions
            if (SliceGrabbers[s].InnerGrabbers != null && SliceGrabbers[s].InnerGrabbers.Count > 0)
            {
                SliceGrabbers[s].InnerDestinations = new List<Vector3>();
                for (int i = 0; i < SliceGrabbers[s].InnerGrabbers.Count; i++)
                {
                    var InnerGrabber = SliceGrabbers[s].InnerGrabbers[i];
                    SliceGrabbers[s].InnerDestinations.Add(InnerGrabber.transform.position);
                }
            }

            // Initialize OuterDestinations with initial OuterGrabbers positions
            if (SliceGrabbers[s].OuterGrabbers != null && SliceGrabbers[s].OuterGrabbers.Count > 0)
            {
                SliceGrabbers[s].OuterDestinations = new List<Vector3>();
                for (int i = 0; i < SliceGrabbers[s].OuterGrabbers.Count; i++)
                {
                    var OuterGrabber = SliceGrabbers[s].OuterGrabbers[i];
                    SliceGrabbers[s].OuterDestinations.Add(OuterGrabber.transform.position);
                }
            }
        }
    }

    private void InitializeSliceData()
    {
        List<ParticleGrab> AllGrabbers = new List<ParticleGrab>();
        foreach(var g in Grabbers)
        {
            AllGrabbers.Add(g.GetComponent<ParticleGrab>());
        }

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
            data.axis = Slicer.GetAxis(); //Initialize axis so that each slice can project correctly when using triangulation
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
                for (var g = 0; g < s.OuterGrabbers.Count; g++)
                {
                    var Current = s.OuterGrabbers[g].transform.position;
                    var Final = s.OuterDestinations[g]; //Destinations contains Outer grabbers only
                    var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                    s.OuterGrabbers[g].transform.Translate(movement, Space.World);
                    IsFinished = true;
                }

                //Then Handle inner Grabbers
                if (s.InnerGrabbers != null && s.InnerDestinations != null)
                {
                    Debug.Assert(s.InnerGrabbers.Count == s.InnerDestinations.Count);
                    for (var i = 0; i < s.InnerGrabbers.Count; i++)
                    {
                        var Current = s.InnerGrabbers[i].transform.position;
                        var Final = s.InnerDestinations[i];
                        var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                        s.InnerGrabbers[i].transform.Translate(movement, Space.World);
                        IsFinished = true;
                    }
                }
            }
            count++;
        }

        if (!InterpolatedDeformation)
        {
            PrintStatistics();
        }
    }

    private void MoveParticlesPeriodically(SliceData s, int Index)
    {
        Debug.Assert(s.OuterGrabbers.Count == s.OuterDestinations.Count);
        for (int g = 0; g < s.OuterGrabbers.Count; g++)
        {
            var current = s.OuterGrabbers[g].transform.position;
            var final = s.OuterDestinations[g];

            var next = Vector3.MoveTowards(current, final, DeformIteration);
            s.OuterGrabbers[g].transform.position = next;
        }

        //Inner Grabbers
        if (s.InnerGrabbers != null && s.InnerDestinations != null)
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

    public void SaveModel()
    {
        if(Reconstructor != null)
        {
            Reconstructor.SaveMeshFromGrabbers(this);
        }
        else
        {
            Debug.LogWarning("Add a Mesh Reconstructor V2 script to save deformed object");
        }
    }

    private void PrintStatistics()
    {
        if (MeshToCompare == null)
        {
            Debug.LogWarning("Please, Add a meshfilter component for comparison");
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

        //Mesh OriginalNormalized = MeshComparison.NormalizeMesh(MeshToCompare.sharedMesh, transform);
        //Mesh DeformedNormalized = MeshComparison.NormalizeMesh(copy, transform);

        Mesh OriginalNormalized = MeshComparison.NormalizeMeshForEvaluation(MeshToCompare.sharedMesh, transform, MeshToCompare.sharedMesh.bounds.center);
        Mesh DeformedNormalized = MeshComparison.NormalizeMeshForEvaluation(copy, transform, copy.bounds.center);

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
