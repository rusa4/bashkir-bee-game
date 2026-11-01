using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BeeGladePlayCheck
{
    const string Flag = "BeeGlade.PlayCheck";
    static int phase, flowerIndex, hazardHits;
    static double due, started;
    static Vector2 spawn;
    static readonly List<string> checks = new List<string>();
    static BeeGladePlayCheck()
    {
        if (SessionState.GetBool(Flag, false)) EditorApplication.update += Tick;
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeeGlade.unity");
        Directory.CreateDirectory("Verification");
        SessionState.SetBool(Flag, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }
    static T Field<T>(object obj, string name) => (T)obj.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks.Add(message); Debug.Log("BEEGLADE_CHECK_OK: " + message);
    }
    static void Warp(Vector2 point)
    {
        var rb = UnityEngine.Object.FindObjectOfType<PlayerMovement>().GetComponent<Rigidbody2D>();
        rb.velocity = Vector2.zero; rb.position = point; Physics2D.SyncTransforms();
    }
    static void Next(int next, float delay = 0.4f)
    { phase = next; due = EditorApplication.timeSinceStartup + delay; }
    static Crystal[] Flowers()
    {
        var result = UnityEngine.Object.FindObjectsOfType<Crystal>();
        Array.Sort(result, (a,b) => string.CompareOrdinal(a.name, b.name)); return result;
    }
    static void Restart(GameManager manager) => typeof(GameManager).GetMethod("RestartGame",
        BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; due = now + 1.5; }
        if (now < due) return;
        try
        {
            if (now - started > 60) throw new Exception("Play check timed out");
            var manager = GameManager.Instance;
            if (manager == null) throw new Exception("GameManager did not initialize");
            var bee = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            var hive = UnityEngine.Object.FindObjectOfType<TeleportController>();
            if (phase == 0)
            {
                Require(Field<int>(manager, "totalCrystals") == 3, "Automatically counted three flowers");
                Require(Field<int>(manager, "currentHealth") == 3, "Initial health is three");
                Require(hive.GetComponent<Collider2D>().isTrigger, "Closed hive remains a non-blocking trigger");
                foreach (var collider in hive.GetComponentsInChildren<Collider2D>(true))
                    Require(collider.isTrigger, "Hive has no solid colliders");
                Require(bee.GetComponent<Animator>().runtimeAnimatorController != null, "Bee has wing animation");
                Require(bee.GetComponent<CapsuleCollider2D>() != null, "Bee uses a fixed body collider");
                spawn = bee.GetComponent<Rigidbody2D>().position;
                BeeGladePreview.CaptureUi("Verification/felt-ui-gameplay.png");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/gameplay.png"));
                Warp(hive.transform.TransformPoint(hive.GetComponent<BoxCollider2D>().offset));
                Next(10, 0.6f);
            }
            else if (phase == 10)
            {
                Require(!Field<bool>(manager, "isLevelComplete"), "Entering closed hive cannot complete level without pollen");
                Warp(UnityEngine.Object.FindObjectOfType<Hazard>().transform.position);
                Next(1, 0.8f);
            }
            else if (phase == 1)
            {
                Require(Field<int>(manager, "currentHealth") == 2, "Physical contact with web removes one health");
                Require(Vector2.Distance(bee.GetComponent<Rigidbody2D>().position, spawn) < 0.05f,
                    "Web damage returns bee to spawn");
                Require(bee.enabled, "Movement unlocks after respawn");
                BeeGladePreview.CaptureUi("Verification/felt-ui-health.png");
                Next(2, 0);
            }
            else if (phase == 2)
            {
                var flowers = Flowers();
                if (flowerIndex < flowers.Length)
                {
                    var hitbox = flowers[flowerIndex].GetComponent<CircleCollider2D>();
                    Warp(flowers[flowerIndex].transform.TransformPoint(hitbox.offset)); Next(3);
                }
                else
                {
                    Require(hive.GetComponent<Collider2D>().isTrigger, "Collecting all pollen opens hive entrance");
                    var collider = hive.GetComponent<BoxCollider2D>();
                    Warp(hive.transform.TransformPoint(collider.offset)); Next(4, 0.6f);
                }
            }
            else if (phase == 3)
            {
                var flower = Flowers()[flowerIndex];
                Require(flower.IsCollected, "Flower " + flowerIndex + " collects through its physics trigger");
                Require(flower.gameObject.activeSelf, "Collected flower " + flowerIndex + " stays visible");
                Require(!flower.GetComponent<Collider2D>().enabled, "Collected flower cannot be counted twice");
                Require(flower.GetComponent<SpriteRenderer>().sprite.name.EndsWith("EmptyOrOpen"),
                    "Collected flower shows depleted pollen center");
                Require(Field<int>(manager, "collectedCrystals") == flowerIndex + 1, "Pollen counter increments once");
                flowerIndex++; Next(2, 0);
            }
            else if (phase == 4)
            {
                Require(Field<bool>(manager, "isLevelComplete"), "Entering open hive completes level");
                Require(Field<GameObject>(manager, "victoryPanel").activeSelf, "Victory panel is shown");
                Require(!bee.enabled, "Bee movement stops on victory");
                BeeGladePreview.CaptureUi("Verification/felt-ui-victory.png");
                Restart(manager); Next(5, 1);
            }
            else if (phase == 5)
            {
                Require(Field<int>(manager, "currentHealth") == 3 && Field<int>(manager, "collectedCrystals") == 0,
                    "Restart restores health and pollen count");
                Require(hive.GetComponent<Collider2D>().isTrigger, "Restart keeps hive non-blocking");
                Require(Field<GameObject>(hive, "closedVisual").activeSelf &&
                    !Field<GameObject>(hive, "openVisual").activeSelf, "Restart restores closed hive appearance");
                foreach (var flower in Flowers()) Require(!flower.IsCollected && flower.GetComponent<Collider2D>().enabled,
                    "Restart restores flower pollen and hitbox");
                var horizontal = UnityEngine.Object.FindObjectOfType<Hazard>();
                var verticalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BeeGlade/Prefabs/CobwebVertical.prefab");
                Require(verticalPrefab != null, "Vertical cobweb prefab exists");
                var vertical = UnityEngine.Object.Instantiate(verticalPrefab, horizontal.transform.position, Quaternion.identity);
                horizontal.gameObject.SetActive(false);
                var verticalTrigger = vertical.GetComponent<BoxCollider2D>();
                Require(verticalTrigger.isTrigger && verticalTrigger.size.y > verticalTrigger.size.x,
                    "Vertical cobweb has a tall non-solid damage trigger");
                Require(vertical.GetComponent<Hazard>() != null && vertical.GetComponent<SpriteRenderer>().sprite != null,
                    "Vertical cobweb has sprite and damage component");
                BeeGladePreview.CaptureUi("Verification/vertical-web-preview.png");
                Warp(UnityEngine.Object.FindObjectOfType<Hazard>().transform.position); Next(6, 0.8f);
            }
            else if (phase == 6)
            {
                hazardHits++;
                Require(Field<int>(manager, "currentHealth") == 3 - hazardHits, "Repeated web hit " + hazardHits + " updates health");
                if (hazardHits < 3)
                { Warp(UnityEngine.Object.FindObjectOfType<Hazard>().transform.position); Next(6, 0.8f); }
                else
                {
                    Require(Field<bool>(manager, "isGameOver"), "Three web hits cause game over");
                    Require(Field<GameObject>(manager, "gameOverPanel").activeSelf, "Game over panel is shown");
                    BeeGladePreview.CaptureUi("Verification/felt-ui-defeat.png");
                    Restart(manager); Next(7, 1);
                }
            }
            else Finish(true, null);
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    [Serializable] class Report { public bool success; public string unityVersion; public string error; public List<string> checks; }
    static void Finish(bool success, string error)
    {
        SessionState.SetBool(Flag, false);
        EditorApplication.update -= Tick;
        File.WriteAllText("Verification/play-check.json", JsonUtility.ToJson(new Report {
            success = success, unityVersion = Application.unityVersion, error = error, checks = checks }, true));
        if (success) Debug.Log("BEEGLADE_PLAY_CHECK_OK"); else Debug.LogError(error);
        EditorApplication.Exit(success ? 0 : 1);
    }
}
