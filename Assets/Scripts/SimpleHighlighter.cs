using System.Collections.Generic;
using UnityEngine;

public class SimpleHighlighter : MonoBehaviour
{
    Renderer[] rends;
    Material[][] originalSharedMaterials;
    List<Material> createdInstances = new List<Material>();
    bool highlighted = false;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        originalSharedMaterials = new Material[rends.Length][];
        for (int i = 0; i < rends.Length; i++)
            originalSharedMaterials[i] = rends[i].sharedMaterials;
    }

    public void Highlight(Color color, float intensity)
    {
        if (highlighted) return;
        for (int i = 0; i < rends.Length; i++)
        {
            Material[] shared = originalSharedMaterials[i];
            Material[] mats = new Material[shared.Length];
            for (int j = 0; j < shared.Length; j++)
            {
                Material m = new Material(shared[j]);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                mats[j] = m;
                createdInstances.Add(m);
            }
            rends[i].materials = mats;
        }
        highlighted = true;
    }

    public void Unhighlight()
    {
        if (!highlighted) return;
        for (int i = 0; i < rends.Length; i++)
            rends[i].materials = originalSharedMaterials[i];
        for (int i = 0; i < createdInstances.Count; i++)
            Destroy(createdInstances[i]);
        createdInstances.Clear();
        highlighted = false;
    }

    void OnDestroy()
    {
        for (int i = 0; i < createdInstances.Count; i++)
            Destroy(createdInstances[i]);
        createdInstances.Clear();
    }
}
