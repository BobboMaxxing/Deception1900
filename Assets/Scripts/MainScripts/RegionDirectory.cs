using System.Collections.Generic;
using UnityEngine;

public class RegionDirectory : MonoBehaviour
{
    public static RegionDirectory Instance { get; private set; }

    private readonly Dictionary<string, Country> countriesById = new();
    private readonly Dictionary<string, OceanRegion> oceansById = new();

    private bool built = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Rebuild();
    }

    public void Rebuild()
    {
        countriesById.Clear();
        oceansById.Clear();

        foreach (var c in FindObjectsOfType<Country>(true))
        {
            if (c == null || string.IsNullOrWhiteSpace(c.regionId)) continue;
            countriesById[c.regionId] = c;
        }

        foreach (var o in FindObjectsOfType<OceanRegion>(true))
        {
            if (o == null || string.IsNullOrWhiteSpace(o.regionId)) continue;
            oceansById[o.regionId] = o;
        }

        built = true;
        Debug.Log($"[RegionDirectory] Rebuilt. Countries={countriesById.Count}, Oceans={oceansById.Count}");
    }

    private void EnsureBuilt()
    {
        if (!built) Rebuild();
    }

    public Country GetCountryOrNull(string regionId)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(regionId)) return null;
        countriesById.TryGetValue(regionId, out var c);
        return c;
    }

    public OceanRegion GetOceanOrNull(string regionId)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(regionId)) return null;
        oceansById.TryGetValue(regionId, out var o);
        return o;
    }
}
