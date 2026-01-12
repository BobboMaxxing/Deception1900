using UnityEngine;

public class CameraMovment : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] Transform defaultPosition;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] LayerMask countryLayer;
    [SerializeField] LayerMask focusPointLayer;
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

    void CheckCountryClick()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, countryLayer))
        {
            if (!isFocusing)
            {
                FocusOnCountry(hit.transform);
            }
        }
    }

    void FocusOnCountry(Transform country)
    {
        Transform focusPoint = null;

        foreach (Transform child in country)
        {
            if (child.gameObject.layer == LayerMask.NameToLayer("CameraFocusPoint"))
            {
                focusPoint = child;
                break;
            }
        }

        if (focusPoint == null)
        {
            Debug.LogWarning($"No CameraFocusPoint found under {country.name}, using fallback position.");
            targetPosition = defaultPosition.position;
        }
        else
        {
            targetPosition = focusPoint.position + new Vector3(20, 20, 0);
        }

        isFocusing = true;
    }


    Transform FindFocusPointRecursive(Transform obj)
    {
        if (((1 << obj.gameObject.layer) & focusPointLayer) != 0)
            return obj;

        foreach (Transform child in obj)
        {
            Transform found = FindFocusPointRecursive(child);
            if (found != null)
                return found;
        }

        return null;
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
