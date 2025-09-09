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

    void Start()
    {
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
        targetPosition = country.position + new Vector3(4, 20, 10);
    }

    void ResetCamera()
    {
        isFocusing = false;
        targetPosition = defaultPosition.position;
        targetRotation = defaultPosition.rotation;
    }

    void SmoothMoveCamera()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }
}
