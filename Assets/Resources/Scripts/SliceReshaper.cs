using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(BoundsSlicer))]
[RequireComponent(typeof(GrabGenerator))]
public class SliceReshaper : MonoBehaviour
{

    [SerializeField]
    public BoundsSlicer Slicer;

    [SerializeField]
    public GrabGenerator Generator;

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

    //Used for Interpolated deformation only
    [SerializeField]
    private int TotalIterations = 1000;

    //false only if we want to insert data manually 
    public bool Initialize = false;

    private bool DeformLock = false;
    private bool IsFinished = false;

    private int CurrentIteration = 0;

    private const float DeformIteration = 0.001f;

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

            //Testing
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
            //Use this to transform all slices to new area
            x = transform.position.x;
            y = transform.position.y;
            z = transform.position.z;

            //Also, use this for scaling in case scale is not 1
            xs = transform.localScale.x;
            ys = transform.localScale.y;
            zs = transform.localScale.z;

            var Nmin = new Vector3((s.Min.x * xs) + x, (s.Min.y * ys) + y, (s.Min.z * zs) + z);
            var Nmax = new Vector3((s.Max.x * xs) + x, (s.Max.y * ys) + y, (s.Max.z * zs) + z);

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
                Debug.Log("Finished");
                IsFinished = true;
            }
            s.Grabbers[g].transform.position = Vector3.Lerp(Final, Current, CurrentIteration / TotalIterations);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) && !DeformLock)
        {
            Debug.Log("Pressed");
            DeformLock = true;
            DeformSlices();
        }

        //TODO fix this code some other time
        if (!IsFinished && DeformLock && InterpolatedDeformation)
        {
            foreach (var s in SliceGrabbers)
            {
                MoveParticlesPeriodically(s);
            }
        }
    }
}
