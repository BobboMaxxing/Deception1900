using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public int ownerID = -1; // -1 = neutral / unclaimed

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
        UpdateColor();
    }

    private void UpdateColor()
    {
        if (countryRenderer == null) return;

        switch (ownerID)
        {
            case 0:
                countryRenderer.material.color = Color.blue; 
                break;
            case 1:
                countryRenderer.material.color = Color.red; 
                break;
            case 2:
                countryRenderer.material.color = Color.green; 
                break;
            default:
                countryRenderer.material.color = Color.gray; 
                break;
        }
    }
}
