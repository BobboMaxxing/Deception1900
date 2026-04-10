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
    [SerializeField] float keyboardMoveSpeed = 25f;
    [SerializeField] float zoomSpeed = 60f;
    [SerializeField] float minZoomY = 60f;
    [SerializeField] float maxZoomY = 200f;

    [Header("Zoom Tilt")]
    [SerializeField] float maxTiltAngle = 30f;

    [Header("Focus")]
    [SerializeField] bool focusOnCountryClick = true;

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
        }
        else if (targetCamera != null)
        {
            targetPosition = targetCamera.transform.position;
        }

        UpdateZoomTilt();
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

        HandleKeyboardMove();
        HandleScrollZoom();
    }

    void CheckCountryClick()
    {
        if (targetCamera == null) return;
        if (!focusOnCountryClick) return;

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

        UpdateZoomTilt();
    }

    void HandleKeyboardMove()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            horizontal -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            horizontal += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            vertical += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            vertical -= 1f;

        Vector3 input = new Vector3(horizontal, 0f, vertical);
        if (input.sqrMagnitude <= 0.001f)
            return;

        isFocusing = false;

        Vector3 right = targetCamera.transform.right;
        Vector3 forward = targetCamera.transform.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        Vector3 moveDir = (right * input.x + forward * input.z).normalized;
        Vector3 newPos = targetPosition + moveDir * keyboardMoveSpeed * Time.deltaTime;

        newPos.x = Mathf.Clamp(newPos.x, xLimits.x, xLimits.y);
        newPos.z = Mathf.Clamp(newPos.z, zLimits.x, zLimits.y);

        targetPosition = new Vector3(newPos.x, targetPosition.y, newPos.z);
    }
    void HandleScrollZoom()
    {
        if (targetCamera == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
            return;

        isFocusing = false;

        Vector3 newPos = targetPosition;
        newPos.y -= scroll * zoomSpeed * Time.deltaTime * 10f;
        newPos.y = Mathf.Clamp(newPos.y, minZoomY, maxZoomY);

        newPos.x = Mathf.Clamp(newPos.x, xLimits.x, xLimits.y);
        newPos.z = Mathf.Clamp(newPos.z, zLimits.x, zLimits.y);

        targetPosition = newPos;

        UpdateZoomTilt();
    }

    void UpdateZoomTilt()
    {
        // zoom01: 0 = fully zoomed out, 1 = fully zoomed in
        float zoom01 = Mathf.InverseLerp(maxZoomY, minZoomY, targetPosition.y);

        // Tilt: zoomed out = 90 (straight down), zoomed in = 90 - maxTiltAngle
        float tiltAngle = Mathf.Lerp(90f, 90f - maxTiltAngle, zoom01);

        // Only rotate on X axis, Y and Z locked to 0
        targetRotation = Quaternion.Euler(tiltAngle, -90f, 0f);
    }

    public void ResetCamera()
    {
        isFocusing = false;

        if (defaultPosition == null)
            return;

        targetPosition = defaultPosition.position;
        UpdateZoomTilt();
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
        targetRotation = Quaternion.Euler(buildTablePosition.rotation.eulerAngles.x, -90f, 0f);
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
