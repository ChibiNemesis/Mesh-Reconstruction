using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable, Inspectable]
public class ContourData 
{
    public int PartNum; // Number of Total Contours inside a Contour Part
    public float Length; // Length of this Part

    public ContourData(int partNum, float length)
    {
        PartNum = partNum;
        Length = length;
    }

    public ContourData(ContourData data)
    {
        PartNum = data.PartNum;
        Length = data.Length;
    }
}
