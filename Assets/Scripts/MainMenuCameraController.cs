using TMPro;
using UnityEngine;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class MainMenuCameraController : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Transform defaultPosition;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float rotateSpeed = 2f;
    [SerializeField] LayerMask countryLayer;

    [Header("Camera Settings")]
    [SerializeField] Vector3 targetPosition;
    [SerializeField] Quaternion targetRotation;
    [SerializeField] bool isFocusing = false;

    [Header("UI Settings")]
    [SerializeField] TMP_Text selectedCountryText;
    [SerializeField] GameObject bigPowerButton;

    private Transform selectedCountry = null;

    void Start()
    {
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;

        if (selectedCountryText != null)
            selectedCountryText.text = "Select a country";
    }

    void Update()
    {
        HandleInput();
        SmoothMoveCamera();
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetCamera();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            CheckCountryClick();
        }
    }

    void CheckCountryClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, countryLayer))
        {
            SelectCountry(hit.transform);
        }
    }

    void SelectCountry(Transform country)
    {

        selectedCountry = country;

        Country countryScript = country.GetComponent<Country>();
        if (countryScript != null && selectedCountryText != null)
        {
            selectedCountryText.text = "Select Country: " + countryScript.countryName;
        }

        targetPosition = country.position + new Vector3(4, 20, 10);
        isFocusing = true;
    }

    void ResetCamera()
    {
        isFocusing = false;
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;

        if (selectedCountryText != null)
            selectedCountryText.text = "Select a country";
    }

    void SmoothMoveCamera()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    public void OnBigPowerButtonClicked()
    {
        if (selectedCountry != null)
        {
            Country countryScript = selectedCountry.GetComponent<Country>();
            if (countryScript != null)
            {
                Debug.Log("Big Power activated! Country selected: " + countryScript.countryName);
            }
        }
    }
}