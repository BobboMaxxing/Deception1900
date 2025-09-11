using UnityEngine;

public class CameraMovment : MonoBehaviour
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

    [Header("Free Move Settings")]
    [SerializeField] float dragSpeed = 20f;
    [SerializeField] Vector2 xLimits = new Vector2(-50f, 50f);
    [SerializeField] Vector2 zLimits = new Vector2(-50f, 50f);

    [Header("Zoom Settings")]
    [SerializeField] float zoomSpeed = 50f;
    [SerializeField] float minZoom = 10f;
    [SerializeField] float maxZoom = 100f;

    Camera cam;

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

    void FixedUpdate()
    {
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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, countryLayer))
        {
            if (!isFocusing)
            {
                FocusOnCountry(hit.transform);
            }
        }
    }

    void FocusOnCountry(Transform country)
    {
        isFocusing = true;
        targetPosition = country.position + new Vector3(20, 20, 0);
    }

    void ResetCamera()
    {
        isFocusing = false;
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;
    }

    void FreeMove()
    {
        isFocusing = false;

        float moveX = Input.GetAxis("Mouse X") * dragSpeed * 100 * Time.deltaTime;
        float moveZ = Input.GetAxis("Mouse Y") * dragSpeed * 100 * Time.deltaTime;

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
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }
}