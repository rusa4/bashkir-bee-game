using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private Image[] heartImages;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private float respawnLockTime = 0.2f;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Victory")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text starRatingText;

    [Header("Timer")]
    [SerializeField] private TMP_Text timerText;

    [Header("Crystals")]
    [Tooltip("Count flowers placed on the scene when Play starts.")]
    [SerializeField] private bool autoCountFlowers = true;
    [SerializeField] private int totalCrystals = 3;
    [SerializeField] private TMP_Text crystalCounterText;

    [Header("Teleport")]
    [SerializeField] private TeleportController teleport;

    private int currentHealth;
    private int collectedCrystals;

    private float elapsedTime;

    private Vector3 playerStartPosition;
    private Rigidbody2D playerRigidbody;
    private PlayerMovement playerMovement;

    private bool canTakeDamage = true;
    private bool isGameOver;
    private bool isLevelComplete;

    private void Awake()
    {
        Instance = this;

        if (player == null)
        {
            Debug.LogError("Player не назначен в GameManager.");
            enabled = false;
            return;
        }

        if (autoCountFlowers) totalCrystals = FindObjectsOfType<Crystal>().Length;
        currentHealth = maxHealth;
        playerStartPosition = player.position;

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        playerMovement = player.GetComponent<PlayerMovement>();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        UpdateHearts();
        UpdateCrystalCounter();
        UpdateTimerText();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartGame();
            return;
        }

        if (!isGameOver && !isLevelComplete)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerText();
        }
    }

    public void DamagePlayer()
    {
        if (!canTakeDamage || isGameOver || isLevelComplete)
        {
            return;
        }

        canTakeDamage = false;

        currentHealth--;
        UpdateHearts();

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        RespawnPlayer();

        if (currentHealth <= 0)
        {
            EndGame();
            return;
        }

        StartCoroutine(UnlockPlayerAfterRespawn());
    }

    public bool TryCollectCrystal()
    {
        if (isGameOver ||
            isLevelComplete ||
            collectedCrystals >= totalCrystals)
        {
            return false;
        }

        collectedCrystals++;
        UpdateCrystalCounter();

        if (collectedCrystals >= totalCrystals)
        {
            if (teleport != null)
            {
                teleport.OpenTeleport();
            }
            else
            {
                Debug.LogError("Teleport не назначен в GameManager.");
            }
        }

        return true;
    }

    public void CompleteLevel()
    {
        if (isGameOver || isLevelComplete)
        {
            return;
        }

        isLevelComplete = true;
        canTakeDamage = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector2.zero;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }
        UpdateStarRating();
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    private void RespawnPlayer()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;

            playerRigidbody.position = new Vector2(
                playerStartPosition.x,
                playerStartPosition.y
            );
        }
        else
        {
            player.position = playerStartPosition;
        }
    }

    private IEnumerator UnlockPlayerAfterRespawn()
    {
        yield return new WaitForSeconds(respawnLockTime);

        yield return new WaitUntil(() =>
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) < 0.01f &&
            Mathf.Abs(Input.GetAxisRaw("Vertical")) < 0.01f
        );

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        canTakeDamage = true;
    }

    private void EndGame()
    {
        isGameOver = true;
        canTakeDamage = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector2.zero;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    private void RestartGame()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
    private void UpdateStarRating()
    {
        if (starRatingText == null)
        {
            return;
        }

        if (elapsedTime < 10f)
        {
            starRatingText.text = "★★★";
        }
        else if (elapsedTime < 15f)
        {
            starRatingText.text = "★★☆";
        }
        else if (elapsedTime <= 20f)
        {
            starRatingText.text = "★☆☆";
        }
        else
        {
            starRatingText.text = "☆☆☆";
        }
    }
    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            timerText.text = $"Время: {elapsedTime:F1} с";
        }
    }

    private void UpdateCrystalCounter()
    {
        if (crystalCounterText != null)
        {
            crystalCounterText.text =
                $"Пыльца: {collectedCrystals}/{totalCrystals}";
        }
    }

    private void UpdateHearts()
    {
        for (int i = 0; i < heartImages.Length; i++)
        {
            heartImages[i].sprite =
                i < currentHealth ? fullHeart : emptyHeart;
        }
    }
}
