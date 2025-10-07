using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Custom class to turn a sphere to a cube particles
[RequireComponent(typeof(SliceReshaper))]
public class SphereToCube : SliceInitializer
{
    [SerializeField]
    public enum CubePart { TOP, BOTTOM, MIDDLE};

    [SerializeField]
    public List<GameObject> Grabbers;

    [SerializeField]
    public SliceReshaper shaper;

    private int TopIndex = -1, BottomIndex = -1;
    private const float MIN_VAL = -1000000f;
    private const float MAX_VAL = 1000000f;

    void Start()
    {
        Grabbers = new List<GameObject>();
        if (shaper == null)
            shaper = GetComponent<SliceReshaper>();

        Grabbers = shaper.Grabbers;
    }

    public override void InitializeSlices()
    {
        FindMinMaxSlice();

        InitializeBottom();
        InitializeTop();
        InitializeMiddle();
    }

    //Used to find index of lower and higher slice
    private void FindMinMaxSlice()
    {
        var TopValue = MIN_VAL;
        var BottomValue = MAX_VAL;
        var Axis = shaper.Slicer.GetAxis();
        //Find Slice with Top Index
        for (var a = 0; a < shaper.SliceGrabbers.Count; a++)
        {
            var Slice = shaper.SliceGrabbers[a];

            Bounds sb = new Bounds();
            sb.SetMinMax(Slice.Min, Slice.Max);

            Vector3 center = sb.center;

            if (Axis == AxisCut.X)
            {
                if (center.x > TopValue)
                {
                    TopIndex = a;
                }
                if(center.x < BottomValue)
                {
                    BottomIndex = a;
                }
            }
            else if (Axis == AxisCut.Y)
            {
                if (center.y > TopValue)
                {
                    TopIndex = a;
                }
                if (center.y < BottomValue)
                {
                    BottomIndex = a;
                }
            }
            else
            { // Z Axis
                if (center.z > TopValue)
                {
                    TopIndex = a;
                }
                if (center.z < BottomValue)
                {
                    BottomIndex = a;
                }
            }
        }
    }

    //Initialize Top Slice of the sphere
    private void InitializeTop()
    {
        var Axis = shaper.Slicer.GetAxis();

        //Start searching final position of particles
        var TopSlice = shaper.SliceGrabbers[TopIndex];
        var HighValue = MIN_VAL;
        //Now search for the grabber with the highest value on a specific axis
        for(var b = 0; b < TopSlice.Grabbers.Count; b++)
        {
            var g = TopSlice.Grabbers[b].transform.position;
            if(Axis==AxisCut.X && g.x > HighValue)
            {
                HighValue = g.x;
            }
            else if (Axis == AxisCut.Y && g.y > HighValue)
            {
                HighValue = g.y;
            }
            else if(g.z > HighValue)
            {
                HighValue = g.z;
            }
        }

        //All top grabbers go up
        foreach(var grab in TopSlice.Grabbers)
        {
            var pos = grab.transform.position;
            if (Axis == AxisCut.X)
            {
                TopSlice.Destinations.Add(new Vector3(HighValue, pos.y, pos.z));
            }
            else if (Axis == AxisCut.Y)
            {
                TopSlice.Destinations.Add(new Vector3(pos.x, HighValue, pos.z));
            }
            else
            {
                TopSlice.Destinations.Add(new Vector3(pos.x, pos.y, HighValue));
            }
        }

        //Now all top slices should be on the same level
        //Next, find the slices needed for corners
        Vector3 Corner_LeftTop, Corner_RightTop, Corner_LeftBottom, Corner_RightBottom;

        if (Axis == AxisCut.X)
        {
            Corner_LeftTop = new Vector3(TopSlice.Max.x,TopSlice.Min.y, TopSlice.Max.z);
            Corner_RightTop = new Vector3(TopSlice.Max.x, TopSlice.Max.y, TopSlice.Max.z);
            Corner_LeftBottom = new Vector3(TopSlice.Max.x, TopSlice.Min.y, TopSlice.Min.z);
            Corner_RightBottom = new Vector3(TopSlice.Max.x, TopSlice.Max.y, TopSlice.Min.z);
        }
        else if (Axis == AxisCut.Y)
        {
            Corner_LeftTop = new Vector3(TopSlice.Min.x, TopSlice.Max.y, TopSlice.Max.z);
            Corner_RightTop = new Vector3(TopSlice.Max.x, TopSlice.Max.y, TopSlice.Max.z);
            Corner_LeftBottom = new Vector3(TopSlice.Min.x, TopSlice.Max.y, TopSlice.Min.z);
            Corner_RightBottom = new Vector3(TopSlice.Max.x, TopSlice.Max.y, TopSlice.Min.z);
        }
        else
        {
            Corner_LeftTop = new Vector3(TopSlice.Min.x, TopSlice.Max.y, TopSlice.Max.z);
            Corner_RightTop = new Vector3(TopSlice.Max.x, TopSlice.Max.y, TopSlice.Max.z);
            Corner_LeftBottom = new Vector3(TopSlice.Min.x, TopSlice.Min.y, TopSlice.Max.z);
            Corner_RightBottom = new Vector3(TopSlice.Max.x, TopSlice.Min.y, TopSlice.Max.z);
        }

        Vector3[] Corners = new Vector3[4] {Corner_LeftTop, Corner_RightTop, Corner_LeftBottom, Corner_RightBottom };
        int[] ClosestIndices = new int[4] {-1, -1, -1, -1 };
        float[] ClosestDistances = new float[4] { MAX_VAL, MAX_VAL, MAX_VAL, MAX_VAL};

        //For each corner, find the particle closest to it
        for (int g = 0; g < TopSlice.Grabbers.Count; g++)
        {
            var ParticlePos = TopSlice.Grabbers[g].transform.position;
            for(int c=0; c<ClosestIndices.Length; c++)
            {
                var dist = Vector3.Distance(Corners[c], ParticlePos);
                if (dist < ClosestDistances[c])
                {
                    ClosestIndices[c] = g;
                    ClosestDistances[c] = dist;
                }
            }
        }

        //Corners final destination
        foreach(var index in ClosestIndices)
        {
            var point = Corners[index];
            TopSlice.Destinations[index].Set(point.x, point.y, point.z);
        }

        //Now do something for the rest of the slices
    }

    //Initialize Bottom Slice of the sphere similar to top
    private void InitializeBottom()
    {
        var Axis = shaper.Slicer.GetAxis();


        var BottomSlice = shaper.SliceGrabbers[TopIndex];
        var LowValue = MIN_VAL;

        for (var b = 0; b < BottomSlice.Grabbers.Count; b++)
        {
            var g = BottomSlice.Grabbers[b].transform.position;
            if (Axis == AxisCut.X && g.x > LowValue)
            {
                LowValue = g.x;
            }
            else if (Axis == AxisCut.Y && g.y > LowValue)
            {
                LowValue = g.y;
            }
            else if (g.z > LowValue)
            {
                LowValue = g.z;
            }
        }

        //All Bottom grabbers go down
        foreach (var grab in BottomSlice.Grabbers)
        {
            var pos = grab.transform.position;
            if (Axis == AxisCut.X)
            {
                BottomSlice.Destinations.Add(new Vector3(LowValue, pos.y, pos.z));
            }
            else if (Axis == AxisCut.Y)
            {
                BottomSlice.Destinations.Add(new Vector3(pos.x, LowValue, pos.z));
            }
            else
            {
                BottomSlice.Destinations.Add(new Vector3(pos.x, pos.y, LowValue));
            }
        }
    }

    //Initialize middle Slices of sphere
    private void InitializeMiddle()
    {

    }
}
