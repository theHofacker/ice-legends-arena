using UnityEngine;

public class TestPlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;

            // Move the object based on input
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
            transform.position += movement;
        }
    }
}
