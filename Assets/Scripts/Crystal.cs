using UnityEngine;

public class Crystal : MonoBehaviour
{
    [Header("Flower appearance")]
    [SerializeField] private Sprite pollenSprite;
    [SerializeField] private Sprite depletedSprite;
    [SerializeField] private SpriteRenderer visual;

    private bool isCollected;

    public bool IsCollected => isCollected;

    private void Awake()
    {
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        if (visual != null && pollenSprite != null) visual.sprite = pollenSprite;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected || !other.CompareTag("Player"))
        {
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.TryCollectCrystal())
        {
            isCollected = true;
            if (visual != null && depletedSprite != null)
            {
                visual.sprite = depletedSprite;
                foreach (var hitbox in GetComponents<Collider2D>()) hitbox.enabled = false;
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
