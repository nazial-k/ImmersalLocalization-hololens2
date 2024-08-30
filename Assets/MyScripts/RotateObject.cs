using UnityEngine;

public class RotateObject : MonoBehaviour
{
    public float rotationSpeed = 10f;

    void Update()
    {
        // Rotate the object around its Y axis
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
    }
}