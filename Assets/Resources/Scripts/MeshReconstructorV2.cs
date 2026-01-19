using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MeshReconstructorV2 : MonoBehaviour
{
    [SerializeField]
    private MeshFilter targetMeshFilter;

    [SerializeField]
    public List<ParticleGrab> grabbers;

    [SerializeField]
    private string saveFolder = "Assets/GeneratedMeshes";

    [SerializeField]
    private string meshName = "ReconstructedModel";

    // Cache the transform state as it was BEFORE physics/play-mode moved it
    private Matrix4x4 initialWorldToLocalMatrix;
    private Matrix4x4 initialLocalToWorldMatrix;
    private bool isMatrixInitialized = false;

    private void Awake()
    {
        // Capture the coordinate system of the object exactly as it is in the Editor
        if (targetMeshFilter != null)
        {
            initialWorldToLocalMatrix = targetMeshFilter.transform.worldToLocalMatrix;
            initialLocalToWorldMatrix = targetMeshFilter.transform.localToWorldMatrix;
            isMatrixInitialized = true;
        }
    }

#if UNITY_EDITOR
    static bool registeredForCallback = false;

    [InitializeOnLoadMethod]
    private static void RegisterPlayModeCallback()
    {
        if (registeredForCallback) return;
        registeredForCallback = true;

        EditorApplication.playModeStateChanged += (state) =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                var reconstructors = GameObject.FindObjectsByType<MeshReconstructor>(FindObjectsSortMode.None);
                foreach (var recon in reconstructors)
                {
                    if (recon != null) recon.SaveMeshFromGrabbers(auto: true);
                }
            }
        };
    }
#endif

    public void SaveMeshFromGrabbers(bool auto = false)
    {
        if (targetMeshFilter == null || !isMatrixInitialized)
        {
            if (!auto) Debug.LogError("Not ready to save. Missing MeshFilter or Matrix not initialized.");
            return;
        }

        Mesh deformed = RebuildFromGrabbers();
        if (deformed == null) return;

#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            // Ensure parent folders exist or creates simple one level
            Directory.CreateDirectory(saveFolder);
        }

        string meshPath = $"{saveFolder}/{meshName}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (existing == null)
        {
            AssetDatabase.CreateAsset(deformed, meshPath);
        }
        else
        {
            existing.Clear();
            EditorUtility.CopySerialized(deformed, existing);
            EditorUtility.SetDirty(existing);
        }

        AssetDatabase.SaveAssets();
        targetMeshFilter.sharedMesh = existing; // Assign the saved asset

        if (!auto) Debug.Log($"[MeshReconstructor] Saved to {meshPath}");
#endif
    }

    private Mesh RebuildFromGrabbers()
    {
        Mesh original = targetMeshFilter.sharedMesh;
        if (original == null) return null;

        Mesh meshCopy = Instantiate(original);
        Vector3[] baseVertices = original.vertices; // These are in Local Space
        Vector3[] newVerts = new Vector3[baseVertices.Length];

        // We will store the DISPLACEMENT in World Space, then transform it to the Initial Local Space
        Vector3[] accumulatedWorldOffsets = new Vector3[newVerts.Length];

        // 1. Calculate World Space Offsets based on Grabbers
        foreach (var grabber in grabbers)
        {
            if (grabber == null) continue;
            List<int> controlledVerts = grabber.GetMeshVertices();
            if (controlledVerts == null) continue;

            Vector3 initialWorld = grabber.GetInitialPosition();
            Vector3 finalWorld = grabber.transform.position;
            Vector3 worldDiff = finalWorld - initialWorld;

            foreach (int vidx in controlledVerts)
            {
                // This assumes rigid linking (vertex moves exactly as much as grabber)
                accumulatedWorldOffsets[vidx] += worldDiff;
            }
        }

        // 2. Apply offsets and transform back to the ORIGINAL (Editor) Local Space
        for (int i = 0; i < baseVertices.Length; i++)
        {
            // A. Convert original local vertex to Start-Frame World Space
            Vector3 originalWorldPos = initialLocalToWorldMatrix.MultiplyPoint3x4(baseVertices[i]);

            // B. Apply the calculated displacement
            Vector3 newWorldPos = originalWorldPos + accumulatedWorldOffsets[i];

            // C. Convert back to Local Space using the Start-Frame Matrix
            // This ensures that when the Transform resets on Stop, the vertex lands in the right spot.
            newVerts[i] = initialWorldToLocalMatrix.MultiplyPoint3x4(newWorldPos);
        }

        meshCopy.vertices = newVerts;
        meshCopy.RecalculateNormals();
        meshCopy.RecalculateBounds();

        return meshCopy;
    }
}
