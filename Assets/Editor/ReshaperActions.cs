using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ReshaperActions))]
public class ReshaperActions : Editor
{
    [SerializeField]
    SliceReshaper shaper;

    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Reshape"))
            shaper.DeformSlices();
        if (GUILayout.Button("Save Object"))
            Debug.Log("Save new game object");
    }
}
