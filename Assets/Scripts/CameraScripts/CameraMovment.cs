using UnityEngine;

public class CameraMovment : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Transform defaultPosition;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] LayerMask countryLayer;
    [SerializeField] Vector3 focusOffset = new Vector3(20f, 20f, 0f);

    [Header("Free Move Settings")]
    [SerializeField] float dragSpeed = 20f;
    [SerializeField] Vector2 xLimits = new Vector2(-50f, 50f);
    [SerializeField] Vector2 zLimits = new Vector2(-50f, 50f);

    Camera cam;
    Vector3 targetPosition;
    Quaternion targetRotation;
    bool isFocusing = false;

    void Start()
    {
        cam = Camera.main;
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;
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

        if (Input.GetMouseButton(2))
        {
            FreeMove();
        }
    }

    private Country GetCountryFromHit(RaycastHit hit)
    {
        if (hit.collider == null) return null;

        Country c = hit.collider.GetComponentInParent<Country>();
        if (c != null) return c;

        c = hit.collider.GetComponent<Country>();
        if (c != null) return c;

        c = hit.collider.GetComponentInChildren<Country>();
        return c;
    }

    void CheckCountryClick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
        {
            if (isFocusing) return;

            Country clickedCountry = GetCountryFromHit(hit);
            if (clickedCountry == null)
            {
                Debug.LogWarning($"Clicked object {hit.collider.name} has no Country component (parent/child lookup failed).");
                return;
            }

            FocusOnCountry(clickedCountry);
        }
    }

    void FocusOnCountry(Country country)
    {
        if (country == null) return;

        targetPosition = country.centerWorldPos + focusOffset;
        isFocusing = true;
    }

    public void ResetCamera()
    {
        isFocusing = false;
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;
    }

    void FreeMove()
    {
        isFocusing = false;

        float moveX = Input.GetAxis("Mouse X") * dragSpeed * Time.deltaTime * 1000;
        float moveZ = Input.GetAxis("Mouse Y") * dragSpeed * Time.deltaTime * 1000;

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        right.y = 0;
        forward.y = 0;
        right.Normalize();
        forward.Normalize();

        Vector3 moveDir = right * -moveX + forward * -moveZ;
        Vector3 newPos = transform.position + moveDir;

        newPos.x = Mathf.Clamp(newPos.x, xLimits.x, xLimits.y);
        newPos.z = Mathf.Clamp(newPos.z, zLimits.x, zLimits.y);

        targetPosition = newPos;
    }

    void SmoothMoveCamera()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
    }
}