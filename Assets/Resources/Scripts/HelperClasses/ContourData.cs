using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable, Inspectable]
public class ContourData 
{
    [System.Serializable, Inspectable]
    public class ContourPart
    {
        public int PartNum; // Number of Total Contours inside a Contour Part
        public bool IsCustomLength = true;
        public float Length; // Length of this Part
    }

    //Store Each Contour's Total Slice List
    public List<ContourPart> ContourParts;
}
