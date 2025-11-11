using UnityEngine;

using UnityEngine;

public class Country : MonoBehaviour
{
    public string countryName;
    public int ownerID = -1;
    public bool isStarterCountry = true;

    [Header("Supply System")]
    public bool isSupplyCenter = false;

    public bool CanBeSelected() => isStarterCountry && ownerID == -1;

    public void SetOwner(int newOwnerID)
    {
        ownerID = newOwnerID;
        Debug.Log($"{countryName} now owned by Player {ownerID}");

        if (MainGameManager.Instance != null)
        {
            MainGameManager.Instance.CheckWinConditionServer();
        }
    }
}