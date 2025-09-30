using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerController : MonoBehaviour
{
    public string playerName = "Player";
    public int playerID;
    public string chosenCountry;

    [SerializeField] private LayerMask countryLayer;
    [SerializeField] private Camera playerCamera;

    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text selectedCountryText;

    [SerializeField] private CameraMovment cameraMovment;
    [SerializeField] private GameObject cameraManager;
    [SerializeField] UnitManager unitManager;

    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightIntensity = 1.2f;

    public bool hasChosenCountry = false;
    private string pendingCountry;
    private List<GameObject> highlightedObjects = new List<GameObject>();

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (cameraMovment == null) cameraMovment = FindObjectOfType<CameraMovment>();
        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(false);
            confirmButton.onClick.AddListener(ConfirmCountryChoice);
        }
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(false);
            cancelButton.onClick.AddListener(CancelCountryChoice);
        }
    }

    void Update()
    {
        if (!hasChosenCountry) HandleCountrySelection();
    }

    void HandleCountrySelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, countryLayer))
            {
                pendingCountry = hit.transform.name;
                if (selectedCountryText != null) selectedCountryText.text = "Selected: " + pendingCountry;
                if (confirmButton != null) confirmButton.gameObject.SetActive(true);
                if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            }
        }
    }

    public void ConfirmCountryChoice()
    {
        if (string.IsNullOrEmpty(pendingCountry)) return;

        chosenCountry = pendingCountry;
        hasChosenCountry = true;

        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (selectedCountryText != null) selectedCountryText.text = "Chosen: " + chosenCountry;
        if (cameraManager != null) cameraManager.SetActive(false);

        HighlightChosenCountryObjects();

        if (unitManager != null)
        {
            unitManager.SpawnUnitsForCountry(chosenCountry, playerID);
        }
    }

    public void CancelCountryChoice()
    {
        pendingCountry = "";
        if (selectedCountryText != null) selectedCountryText.text = "Selection cleared";
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (cameraMovment != null)
        {
            cameraMovment.ResetCamera();
        }
    }

    void HighlightChosenCountryObjects()
    {
        ClearHighlights();
        GameObject[] objs = null;
        try
        {
            objs = GameObject.FindGameObjectsWithTag(chosenCountry);
        }
        catch
        {
            List<GameObject> found = new List<GameObject>();
            GameObject[] all = Object.FindObjectsOfType<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == chosenCountry) found.Add(all[i]);
            }
            objs = found.ToArray();
        }
        for (int i = 0; i < objs.Length; i++)
        {
            GameObject obj = objs[i];
            if (obj == null) continue;
            SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>();
            if (high == null) high = obj.AddComponent<SimpleHighlighter>();
            high.Highlight(highlightColor, highlightIntensity);
            highlightedObjects.Add(obj);
        }
    }

    public void ClearHighlights()
    {
        for (int i = 0; i < highlightedObjects.Count; i++)
        {
            GameObject obj = highlightedObjects[i];
            if (obj == null) continue;
            SimpleHighlighter high = obj.GetComponent<SimpleHighlighter>();
            if (high != null) high.Unhighlight();
        }
        highlightedObjects.Clear();
    }

    public PlayerData GetPlayerData()
    {
        return new PlayerData(playerName, playerID, chosenCountry);
    }
}

[System.Serializable]
public struct PlayerData
{
    public string playerName;
    public int playerID;
    public string country;

    public PlayerData(string name, int id, string country)
    {
        this.playerName = name;
        this.playerID = id;
        this.country = country;
    }
}
