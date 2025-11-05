#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[ExecuteAlways]
public class MeshReconstructor : MonoBehaviour
{
    [SerializeField] 
    private MeshFilter targetMeshFilter;

    [SerializeField] 
    public List<ParticleGrab> grabbers; // All active grabbers

    [SerializeField] 
    private string saveFolder = "Assets/GeneratedMeshes";

    [SerializeField] 
    private string meshName = "ReconstructedModel";

    private bool IsFinished = false;
    private bool Done = false;

    public void SetFinished(bool val = true)
    {
        IsFinished = val;
    }
    public bool IsReconstructionDone()
    {
        return Done;
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
            // IMPORTANT: do the reconstruction immediately when Play is exiting (before domain reload)
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Find active reconstructors and invoke immediate save (they still contain Play-mode transforms)
                var reconstructors = GameObject.FindObjectsOfType<MeshReconstructor>();
                foreach (var recon in reconstructors)
                {
                    if (recon != null)
                    {
                        recon.SaveMeshFromGrabbers(auto: true);
                    }
                }
            }
        };
    }
#endif

    /// <summary>
    /// Rebuilds the mesh using grabber final positions (reads transforms while still in Play).
    /// </summary>
    public void SaveMeshFromGrabbers(bool auto = false)
    {
        if (targetMeshFilter == null)
        {
            if (!auto) Debug.LogError("Missing MeshFilter reference.");
            return;
        }

        if (grabbers == null || grabbers.Count == 0)
        {
            if (!auto) Debug.LogWarning("No grabbers assigned — nothing to reconstruct.");
            return;
        }

        Mesh deformed = RebuildFromGrabbers();
        if (deformed == null)
        {
            Debug.LogError("Failed to rebuild mesh from grabbers.");
            return;
        }

#if UNITY_EDITOR
        // ensure folder
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            Directory.CreateDirectory(saveFolder);
            AssetDatabase.Refresh();
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
        AssetDatabase.Refresh();

        targetMeshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (auto)
            Debug.Log($"[MeshReconstructor] Auto-saved reconstructed mesh from grabbers: {meshPath}");
        else
            Debug.Log($"[MeshReconstructor] Saved reconstructed mesh from grabbers at {meshPath}");
#else
        Debug.LogWarning("Saving meshes as assets is only supported in the Unity Editor.");
#endif
    }

    /// <summary>
    /// Build the new vertex array from the original mesh plus grabber displacements.
    /// Uses local-space differences (mesh local space) so rotation/scale/translation are respected.
    /// </summary>
    private Mesh RebuildFromGrabbers()
    {
        Mesh original = targetMeshFilter.sharedMesh;
        if (original == null)
        {
            Debug.LogError("Target MeshFilter has no mesh assigned.");
            return null;
        }

        // Create a fresh copy from the original shared asset (prevents cumulative changes)
        Mesh meshCopy = Instantiate(original);
        Vector3[] baseVertices = original.vertices;      // original local-space vertices
        Vector3[] newVerts = new Vector3[baseVertices.Length];
        baseVertices.CopyTo(newVerts, 0);

        // accumulate offsets per vertex (in mesh local space)
        Vector3[] accumulatedOffsets = new Vector3[newVerts.Length];

        Transform meshTransform = targetMeshFilter.transform;

        // For each grabber, compute local-space displacement and add to its vertex indices
        foreach (var grabber in grabbers)
        {
            if (grabber == null) continue;

            List<int> controlledVerts = grabber.GetMeshVertices();
            if (controlledVerts == null || controlledVerts.Count == 0) continue;

            // Get initial and final world positions from grabber
            Vector3 initialWorld = grabber.GetInitialPosition();
            Vector3 finalWorld = grabber.transform.position;

            // Convert both to mesh local space (handles mesh transform position, rotation and scale)
            Vector3 initialLocal = meshTransform.InverseTransformPoint(initialWorld);
            Vector3 finalLocal = meshTransform.InverseTransformPoint(finalWorld);

            Vector3 localDiff = finalLocal - initialLocal;

            // Add the localDiff to all controlled vertex indices
            foreach (int vidx in controlledVerts)
            {
                if (vidx >= 0 && vidx < accumulatedOffsets.Length)
                {
                    accumulatedOffsets[vidx] += localDiff;
                }
            }
        }

        // Apply accumulated offsets to base vertices
        for (int i = 0; i < newVerts.Length; i++)
        {
            newVerts[i] = newVerts[i] + accumulatedOffsets[i];
        }

        meshCopy.vertices = newVerts;
        meshCopy.RecalculateNormals();
        meshCopy.RecalculateBounds();

        return meshCopy;
    }
}
