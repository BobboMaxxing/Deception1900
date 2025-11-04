using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public int ownerID = -1;
    public bool isStarterCountry = true;

    public bool CanBeSelected() => isStarterCountry && ownerID == -1;

    public void SetOwner(int newOwnerID)
    {
        ownerID = newOwnerID;
        Debug.Log($"{countryName} now owned by Player {ownerID}");
    }
}
