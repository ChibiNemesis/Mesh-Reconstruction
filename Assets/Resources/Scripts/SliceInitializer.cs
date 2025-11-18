using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Base class with a method called by the Slice Reshaper
public class SliceInitializer : MonoBehaviour
{
    public enum SamplingMode { NONE, UNIFORM, RANDOMIZED }

    //Sampling Method for Sampling across contour's perimeter
    [SerializeField]
    public SamplingMode SamplingMethod = SamplingMode.UNIFORM;

    public virtual void InitializeSlices() { }
}
