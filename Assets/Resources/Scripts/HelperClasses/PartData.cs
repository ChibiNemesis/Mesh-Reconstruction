using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Class Representing the number of Parts we need to separate a single model
/// By default we have one Part
/// </summary>
public class PartData
{
    //Number of Parts
    //How many slices each part contains
    public int PartCount;
    public List<int> Contours;


    // Default: 1 Part
    public PartData()
    {
        PartCount = 1;
    }

    // Any number of Parts
    public PartData(int partCount, List<int> contours)
    {
        PartCount = partCount;
        Contours = contours;

        if(PartCount != Contours.Count)
        {
            Debug.LogWarning("Part Count Does not match Number of Contours. Switching to single Part");
            PartCount = 1;
            int total = contours.Sum();
            Contours = new List<int>{ total };
        }
    }

    public void SetContours(List<int> _contours)
    {
        Contours = _contours;
    }
}
