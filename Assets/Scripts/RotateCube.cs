using UnityEngine;

public class RotateCube : MonoBehaviour
{
    [SerializeField]
    private Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    private void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}
