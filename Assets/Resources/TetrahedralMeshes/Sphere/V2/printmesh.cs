using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class printmesh : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var mesh = GetComponent<MeshFilter>().mesh.vertices;
        List<Vector3> uniques = new List<Vector3>();

        foreach(var ver in mesh)
        {
            if (!uniques.Contains(ver))
            {
                uniques.Add(ver);
            }
        }
        foreach(var ver in uniques)
        {
            Debug.Log("ver: (" + ver.x+", "+ver.y+", "+ver.z+")");
        }
    }
}
