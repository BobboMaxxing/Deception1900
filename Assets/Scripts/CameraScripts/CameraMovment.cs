using UnityEngine;

public class CameraMovment : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Transform defaultPosition;
    [SerializeField] Transform buildTablePosition;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] LayerMask countryLayer;
    [SerializeField] Vector3 focusOffset = new Vector3(20f, 20f, 0f);
    [SerializeField] private Camera targetCamera;

    [Header("Free Move Settings")]
    [SerializeField] float dragSpeed = 20f;
    [SerializeField] Vector2 xLimits = new Vector2(-50f, 50f);
    [SerializeField] Vector2 zLimits = new Vector2(-50f, 50f);

    Camera cam;
    bool lockManualInput = false;
    Vector3 targetPosition;
    Quaternion targetRotation;
    bool isFocusing = false;
    bool allowFocusClick = true;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (defaultPosition != null)
        {
            targetPosition = defaultPosition.position;
            targetRotation = defaultPosition.rotation;
        }
        else if (targetCamera != null)
        {
            targetPosition = targetCamera.transform.position;
            targetRotation = targetCamera.transform.rotation;
        }
    }

    void Update()
    {
        HandleInput();
        SmoothMoveCamera();
    }

    void HandleInput()
    {
        if (lockManualInput)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetCamera();
        }
        else if (allowFocusClick && Input.GetMouseButtonDown(0))
        {
            CheckCountryClick();
        }

        if (Input.GetMouseButton(2))
        {
            FreeMove();
        }
    }

    void CheckCountryClick()
    {
        if (targetCamera == null) return;
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer, QueryTriggerInteraction.Collide))
        {
            if (!isFocusing)
            {
                FocusOnCountry(hit.transform);
            }
        }
    }

    void FocusOnCountry(Transform countryTransform)
    {
        Country country = countryTransform.GetComponentInParent<Country>();
        if (country == null)
            country = countryTransform.GetComponentInChildren<Country>();

        if (country == null)
        {
            Debug.LogWarning("Clicked object has no Country component.");
            return;
        }

        targetPosition = country.centerWorldPos + focusOffset;
        isFocusing = true;
    }

    public void ResetCamera()
    {
        isFocusing = false;

        if (defaultPosition == null)
            return;

        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;
    }
    public void SetTargetCamera(Camera camToUse)
    {
        targetCamera = camToUse;
    }

    public void MoveToBuildTable()
    {
        if (buildTablePosition == null)
            return;

        isFocusing = false;
        targetPosition = buildTablePosition.position;
        targetRotation = buildTablePosition.rotation;
    }

    public void SetFocusClickEnabled(bool value)
    {
        allowFocusClick = value;
    }
    public void SetManualInputLocked(bool value)
    {
        lockManualInput = value;
    }

    void FreeMove()
    {
        if (targetCamera == null) return;

        isFocusing = false;

        float moveX = Input.GetAxis("Mouse X") * dragSpeed * Time.deltaTime * 1000;
        float moveZ = Input.GetAxis("Mouse Y") * dragSpeed * Time.deltaTime * 1000;

        Vector3 right = targetCamera.transform.right;
        Vector3 forward = targetCamera.transform.forward;
        right.y = 0;
        forward.y = 0;
        right.Normalize();
        forward.Normalize();

        Vector3 moveDir = right * -moveX + forward * -moveZ;
        Vector3 newPos = targetCamera.transform.position + moveDir;

        newPos.x = Mathf.Clamp(newPos.x, xLimits.x, xLimits.y);
        newPos.z = Mathf.Clamp(newPos.z, zLimits.x, zLimits.y);

        targetPosition = newPos;
    }

    void SmoothMoveCamera()
    {
        if (targetCamera == null) return;

        targetCamera.transform.position = Vector3.Lerp(targetCamera.transform.position, targetPosition, Time.deltaTime * moveSpeed);
        targetCamera.transform.rotation = Quaternion.Slerp(targetCamera.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);
    }
}
