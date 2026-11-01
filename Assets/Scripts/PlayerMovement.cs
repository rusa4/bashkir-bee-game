using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 4f;

    private Rigidbody2D playerRigidbody;
    private Vector2 movementInput;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movementInput = new Vector2(horizontal, vertical).normalized;
    }

    private void FixedUpdate()
    {
        playerRigidbody.velocity = movementInput * moveSpeed;
    }

    private void OnDisable()
    {
        movementInput = Vector2.zero;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector2.zero;
        }
    }
}