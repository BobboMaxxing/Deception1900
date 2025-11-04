using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public int ownerID = -1;
    public bool isStarterCountry = true;

    [Header("Visual Settings")]
    [SerializeField] private Renderer countryRenderer;

    private void Awake()
    {
        if (countryRenderer == null)
            countryRenderer = GetComponent<Renderer>();
    }

    public void SetOwner(int newOwnerID)
    {
        ownerID = newOwnerID;
    }

    public bool CanBeSelected()
    {
        bool canSelect = isStarterCountry && ownerID == -1;
        Debug.Log($"{countryName} can be selected: {canSelect}");
        return canSelect;
    }


}
