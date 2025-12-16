using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContourData 
{
    public class Contour
    {
        public List<GameObject> Slices;
        public float Length;
    }

    //Store Each Contour's Total Slice List
    public List<Contour> ContourParts;
}
