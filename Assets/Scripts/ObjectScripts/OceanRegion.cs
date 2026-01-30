using System.Collections.Generic;
using UnityEngine;

public class OceanRegion : MonoBehaviour
{
    [Header("Identity")]
    public string regionId;

    [Header("Gameplay Anchor")]
    public Transform centerPoint;

    [Header("Cached World Data")]
    public Vector3 centerWorldPos;

    [Header("Adjacency (manual for now)")]
    public List<OceanRegion> adjacentOceans = new List<OceanRegion>();

    [Header("Coasts (countries bordering this ocean)")]
    public List<Country> coastalCountries = new List<Country>();

    private void Awake()
    {
        if (centerPoint != null) centerWorldPos = centerPoint.position;
        else centerWorldPos = transform.position;
    }
}
