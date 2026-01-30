using UnityEngine;

public static class CountryLookup
{
    public static Country FindCountry(string idOrTag)
    {
        if (string.IsNullOrEmpty(idOrTag)) return null;

        var all = Object.FindObjectsOfType<Country>(true);
        foreach (var c in all)
        {
            if (c == null) continue;

            if (c.gameObject.CompareTag(idOrTag))
                return c;

            if (!string.IsNullOrEmpty(c.countryName) && c.countryName == idOrTag)
                return c;
        }

        return null;
    }

    public static Country FromHit(RaycastHit hit)
    {
        if (hit.collider == null) return null;

        var c = hit.collider.GetComponent<Country>();
        if (c != null) return c;

        c = hit.collider.GetComponentInParent<Country>();
        if (c != null) return c;

        c = hit.collider.GetComponentInChildren<Country>();
        return c;
    }
}
