using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TeleportController : MonoBehaviour
{
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openVisual;

    private Collider2D teleportCollider;
    private bool isOpen;

    private void Awake()
    {
        teleportCollider = GetComponent<Collider2D>();

        closedVisual.SetActive(true);
        openVisual.SetActive(false);

        teleportCollider.isTrigger = true;
        isOpen = false;
    }

    public void OpenTeleport()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;

        closedVisual.SetActive(false);
        openVisual.SetActive(true);

        teleportCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryFinish(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also finish if the last flower was collected while already in the zone.
        TryFinish(other);
    }

    private void TryFinish(Collider2D other)
    {
        if (!isOpen || !other.CompareTag("Player"))
        {
            return;
        }

        GameManager.Instance?.CompleteLevel();
    }
}
