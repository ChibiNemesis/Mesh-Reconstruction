using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ContourCreator : EditorWindow
{
    public Object Object;
    [Range(2, 2)]
    public Object MinPoints;
    [Range(3, 100)]
    public Object MaxPoints;
    [MenuItem("Window/Contour Creator")]
    public static void ShowWindow()
    {
        GetWindow<ParticleGrabGenerator>("Grab Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Object From Scene");
        Object = EditorGUILayout.ObjectField(Object, typeof(GameObject), true);
        EditorGUILayout.LabelField("Minimum Points per Slice");
        MinPoints = EditorGUILayout.ObjectField(MinPoints, typeof(int), true);
        EditorGUILayout.LabelField("Maximum Points per Slice");
        MaxPoints = EditorGUILayout.ObjectField(MaxPoints, typeof(int), true);
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Visualize Contours"))
        {
            //FunctionToRunV2();
        }
    }
}
