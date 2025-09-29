using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoundsSlicer))]
public class SliceReshaper : MonoBehaviour
{
    //Get Slices from slicer
    //Store them somewhere using class
    //Get Axis for the slices
    //based on the axis Get Particles for each slice in clockwise sequence or using a different approach that can be used for CT scans
    //Finally, get the each particle's final  position, and move each one in it's respected final position

    [SerializeField]
    public BoundsSlicer Slicer;

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

    //false only if we want to insert data manually
    public bool Initialize = false;

    private bool DeformLock = false;
    private bool IsFinished = false;

    private const float DeformIteration = 0.005f;

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
            InitializeSliceData();
        }
    }

    private void InitializeSliceData()
    {
        foreach(var s in Slices)
        {
            SliceData data = new SliceData(s.Min, s.Max);
            Bounds b = new Bounds();
            b.SetMinMax(s.Min, s.Max);
            foreach(var g in Grabbers)
            {
                if (b.Contains(g.transform.position))
                {
                    data.Grabbers.Add(g);
                }
            }
            SliceGrabbers.Add(data);
        }
    }

    private void DeformSlices()
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
        for (int g = 0; g < total; g++)
        {
            var Current = s.Grabbers[g].transform.position;
            var Final = s.Destinations[g];
            if (Vector3.Distance(Current, Final) > DeformIteration)
            {
                var newPos = Vector3.MoveTowards(Current, Final, DeformIteration);
                var movement = new Vector3(newPos.x - Current.x, newPos.y - Current.y, newPos.z - Current.z);
                s.Grabbers[g].transform.Translate(movement, Space.World);
            }
            else
            {
                IsFinished = true;
                var movement = new Vector3(Final.x - Current.x, Final.y - Current.y, Final.z - Current.z);
                s.Grabbers[g].transform.Translate(movement, Space.World);
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) && !DeformLock)
        {
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
