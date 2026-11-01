using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D.Sprites;
using TMPro;

public static class BeeGladeSetup
{
    const string Art = "Assets/BeeGlade/Art/";
    const string Prefabs = "Assets/BeeGlade/Prefabs/";
    const string ScenePath = "Assets/Scenes/BeeGlade.unity";
    static Material spriteMaterial;

    public static void Run()
    {
        try
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Bee Glade scene already exists; preserving your layout.");
                return;
            }
            // Retry only our generated assets if an earlier preparation stopped before saving the scene.
            foreach (var name in new[] { "Bee", "Flower", "Bush", "Cobweb", "Hive" })
                AssetDatabase.DeleteAsset(Prefabs + name + ".prefab");
            AssetDatabase.DeleteAsset("Assets/BeeGlade/Animation/Bee_WingFlap.anim");
            AssetDatabase.DeleteAsset("Assets/BeeGlade/Animation/Bee.controller");
            AssetDatabase.DeleteAsset("Assets/BeeGlade/BeeNoFriction.physicsMaterial2D");
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory("Assets/BeeGlade/Animation");
            AssetDatabase.Refresh();
            ImportBee();
            ImportPair("flower-states.png", "Flower", 1.1f);
            ImportPair("koloda-states.png", "Hive", 2.4f);
            ImportSingle("bush.png", "Bush", 1.3f, false);
            ImportSingle("cobweb.png", "Cobweb", 3.3f, true);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainLevel.unity");
            var oldBee = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
            spriteMaterial = oldBee.GetComponent<SpriteRenderer>().sharedMaterial;
            var manager = UnityEngine.Object.FindObjectOfType<GameManager>();
            var hiveOld = UnityEngine.Object.FindObjectOfType<TeleportController>();
            var flowerPositions = UnityEngine.Object.FindObjectsOfType<Crystal>()
                .OrderBy(x => x.name).Select(x => x.transform.position).ToArray();
            var rockObjects = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()
                .Where(x => x.sprite != null && x.sprite.name == "rock").ToArray();
            var bushPositions = rockObjects.Select(x => x.bounds.center).ToArray();
            var hivePosition = hiveOld.transform.position;
            foreach (var flower in UnityEngine.Object.FindObjectsOfType<Crystal>())
                UnityEngine.Object.DestroyImmediate(flower.gameObject);
            foreach (var hazard in UnityEngine.Object.FindObjectsOfType<Hazard>())
                UnityEngine.Object.DestroyImmediate(hazard.gameObject);
            var removed = new HashSet<GameObject>();
            foreach (var rock in rockObjects)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(rock.gameObject);
                if (root == null) root = rock.gameObject;
                if (removed.Add(root)) UnityEngine.Object.DestroyImmediate(root);
            }
            UnityEngine.Object.DestroyImmediate(hiveOld.gameObject);

            var bee = oldBee.gameObject;
            bee.name = "Bee";
            bee.transform.localScale = Vector3.one * 1.15f;
            foreach (var col in bee.GetComponents<Collider2D>()) UnityEngine.Object.DestroyImmediate(col);
            var beeCollider = bee.AddComponent<CapsuleCollider2D>();
            beeCollider.direction = CapsuleDirection2D.Vertical;
            beeCollider.size = new Vector2(0.4f, 0.66f);
            beeCollider.offset = new Vector2(0, 0.015f);
            var noFriction = new PhysicsMaterial2D("BeeNoFriction") { friction = 0, bounciness = 0 };
            AssetDatabase.CreateAsset(noFriction, "Assets/BeeGlade/BeeNoFriction.physicsMaterial2D");
            beeCollider.sharedMaterial = noFriction;
            var beeRenderer = bee.GetComponent<SpriteRenderer>();
            beeRenderer.sprite = Sprites("bee-sheet.png")[0];
            beeRenderer.flipX = false;
            beeRenderer.flipY = false;
            var animator = bee.GetComponent<Animator>();
            if (animator == null) animator = bee.AddComponent<Animator>();
            animator.runtimeAnimatorController = MakeBeeAnimation();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAssetAndConnect(bee, Prefabs + "Bee.prefab", InteractionMode.AutomatedAction);

            var editable = new GameObject("BeeGlade_EditableObjects").transform;
            var flowers = new GameObject("Flowers").transform; flowers.SetParent(editable);
            var bushes = new GameObject("Bushes").transform; bushes.SetParent(editable);
            var flowerPrefab = MakeFlower();
            for (int i = 0; i < flowerPositions.Length; i++)
            {
                var flower = (GameObject)PrefabUtility.InstantiatePrefab(flowerPrefab);
                flower.name = "Flower_" + (i + 1);
                flower.transform.SetParent(flowers);
                flower.transform.position = flowerPositions[i];
            }
            var bushPrefab = MakeBush();
            for (int i = 0; i < bushPositions.Length; i++)
            {
                var bush = (GameObject)PrefabUtility.InstantiatePrefab(bushPrefab);
                bush.name = "Bush_" + (i + 1);
                bush.transform.SetParent(bushes);
                bush.transform.position = bushPositions[i];
            }
            var webPrefab = MakeWeb();
            var web = (GameObject)PrefabUtility.InstantiatePrefab(webPrefab);
            web.transform.SetParent(editable);
            web.transform.position = new Vector3(0, 0, 0);
            var hivePrefab = MakeHive();
            var hive = (GameObject)PrefabUtility.InstantiatePrefab(hivePrefab);
            hive.transform.SetParent(editable);
            // Place near the original exit, allowing room below its base.
            hive.transform.position = new Vector3(hivePosition.x, hivePosition.y - 0.7f, 0);
            var managerSO = new SerializedObject(manager);
            managerSO.FindProperty("teleport").objectReferenceValue = hive.GetComponent<TeleportController>();
            managerSO.FindProperty("autoCountFlowers").boolValue = true;
            managerSO.ApplyModifiedPropertiesWithoutUndo();
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
            {
                if (text.name == "CrystalCounterText") text.text = "Пыльца: 0/3";
            }
            PlayerSettings.productName = "Bee Glade";
            PlayerSettings.companyName = "LocalProject";
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            ValidateScene();
            File.WriteAllText("setup-report.json", "{\"success\":true,\"scene\":\"" + ScenePath +
                "\",\"flowers\":" + flowerPositions.Length + ",\"bushes\":" + bushPositions.Length +
                ",\"webs\":1,\"beeFrames\":12}");
            Debug.Log("BEEGLADE_SETUP_OK: scene, five prefabs and animation ready.");
        }
        catch (Exception e)
        {
            File.WriteAllText("setup-error.txt", e.ToString());
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static TextureImporter Prepare(string filename)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + filename);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = true;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteGenerateFallbackPhysicsShape = true;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return importer;
    }
    static void ImportBee()
    {
        var importer = Prepare("bee-sheet.png");
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 256;
        var entries = new List<SpriteMetaData>();
        for (int i = 0; i < 12; i++) entries.Add(new SpriteMetaData {
            name = "Bee_" + i.ToString("00"), rect = new Rect((i % 4) * 256, (2 - i / 4) * 256, 256, 256),
            alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) });
        ApplySpriteRects(importer, entries.ToArray());
    }
    static RectInt Bounds(Texture2D texture, RectInt region)
    {
        var pixels = texture.GetPixels32();
        int minX = region.xMax, minY = region.yMax, maxX = region.x, maxY = region.y;
        for (int y = region.yMin; y < region.yMax; y++) for (int x = region.xMin; x < region.xMax; x++)
            if (pixels[y * texture.width + x].a >= 100)
            { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    static void ImportPair(string file, string prefix, float targetHeight)
    {
        var importer = Prepare(file);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + file);
        int half = texture.width / 2;
        var a = Bounds(texture, new RectInt(0, 0, half, texture.height));
        var b = Bounds(texture, new RectInt(half, 0, half, texture.height));
        int w = Math.Max(a.width, b.width) + 12, h = Math.Max(a.height, b.height) + 12;
        var entries = new List<SpriteMetaData>();
        foreach (var item in new[] { a, b })
        {
            int i = entries.Count;
            int x = Mathf.Clamp(Mathf.RoundToInt(item.center.x - w / 2f), i * half, (i + 1) * half - w);
            int y = Mathf.Clamp(item.yMin - 6, 0, texture.height - h);
            entries.Add(new SpriteMetaData { name = prefix + (i == 0 ? "_FullOrClosed" : "_EmptyOrOpen"),
                rect = new Rect(x, y, w, h), alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f });
        }
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = h / targetHeight;
        ApplySpriteRects(importer, entries.ToArray());
    }
    static void ImportSingle(string file, string name, float targetWidth, bool hazard)
    {
        var importer = Prepare(file);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + file);
        var bounds = Bounds(texture, new RectInt(0, 0, texture.width, texture.height));
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = bounds.width / targetWidth;
        ApplySpriteRects(importer, new[] { new SpriteMetaData { name = name, rect = new Rect(bounds.x, bounds.y, bounds.width, bounds.height),
            alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f } });
    }
    static void ApplySpriteRects(TextureImporter importer, SpriteMetaData[] metadata)
    {
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new Exception("Sprite Editor data provider unavailable: " + importer.assetPath);
        provider.InitSpriteEditorDataProvider();
        var rects = metadata.Select(m => new SpriteRect {
            name = m.name, rect = m.rect, alignment = (SpriteAlignment)m.alignment,
            pivot = m.pivot, border = m.border, spriteID = GUID.Generate() }).ToArray();
        provider.SetSpriteRects(rects);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null) names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }
    static Sprite[] Sprites(string file) => AssetDatabase.LoadAllAssetsAtPath(Art + file).OfType<Sprite>()
        .OrderBy(s => s.name).ToArray();
    static Sprite FirstState(string file) => Sprites(file).First(s => s.name.EndsWith("FullOrClosed"));
    static Sprite SecondState(string file) => Sprites(file).First(s => s.name.EndsWith("EmptyOrOpen"));
    static GameObject Visual(string name, Sprite sprite, Transform parent = null, int order = 5)
    {
        var go = new GameObject(name); if (parent != null) go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial; renderer.sortingOrder = order;
        return go;
    }
    static GameObject SavePrefab(GameObject obj, string name)
    {
        var asset = PrefabUtility.SaveAsPrefabAsset(obj, Prefabs + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(obj);
        return asset;
    }
    static GameObject MakeFlower()
    {
        var go = Visual("Flower", FirstState("flower-states.png"));
        var trigger = go.AddComponent<CircleCollider2D>(); trigger.isTrigger = true;
        trigger.radius = 0.26f; trigger.offset = new Vector2(0, 0.12f);
        var flower = go.AddComponent<Crystal>(); var so = new SerializedObject(flower);
        so.FindProperty("pollenSprite").objectReferenceValue = FirstState("flower-states.png");
        so.FindProperty("depletedSprite").objectReferenceValue = SecondState("flower-states.png");
        so.FindProperty("visual").objectReferenceValue = go.GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(go, "Flower");
    }
    static GameObject MakeBush()
    {
        var go = Visual("Bush", Sprites("bush.png")[0]);
        go.AddComponent<PolygonCollider2D>();
        return SavePrefab(go, "Bush");
    }
    static GameObject MakeWeb()
    {
        var go = Visual("Cobweb", Sprites("cobweb.png")[0], null, 7);
        var trigger = go.AddComponent<BoxCollider2D>(); trigger.isTrigger = true;
        // The whole silk area is dangerous, including gaps between its threads.
        trigger.size = new Vector2(2.55f, 1.35f); trigger.offset = new Vector2(0, -0.03f);
        go.AddComponent<Hazard>();
        return SavePrefab(go, "Cobweb");
    }
    [MenuItem("Bee Glade/Prepare vertical cobweb prefab")]
    public static void AddVerticalWeb()
    {
        AssetDatabase.Refresh();
        if (File.Exists(Prefabs + "CobwebVertical.prefab")) return;
        var importer = Prepare("cobweb-vertical.png");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "cobweb-vertical.png");
        var bounds = Bounds(texture, new RectInt(0, 0, texture.width, texture.height));
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = bounds.height / 3.3f;
        ApplySpriteRects(importer, new[] { new SpriteMetaData {
            name = "CobwebVertical", rect = new Rect(bounds.x, bounds.y, bounds.width, bounds.height),
            alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f } });
        spriteMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Cobweb.prefab")
            .GetComponent<SpriteRenderer>().sharedMaterial;
        var go = Visual("CobwebVertical", Sprites("cobweb-vertical.png")[0], null, 7);
        var trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.35f, 2.55f);
        go.AddComponent<Hazard>();
        SavePrefab(go, "CobwebVertical");
        AssetDatabase.SaveAssets();
        Debug.Log("BEEGLADE_VERTICAL_WEB_READY");
    }
    static GameObject MakeHive()
    {
        var go = new GameObject("Hive");
        var closed = Visual("ClosedVisual", FirstState("koloda-states.png"), go.transform);
        var open = Visual("OpenVisual", SecondState("koloda-states.png"), go.transform);
        open.SetActive(false);
        // Only the entrance zone detects completion; the hive never blocks movement.
        var entry = go.AddComponent<BoxCollider2D>(); entry.size = new Vector2(0.55f, 0.45f);
        entry.isTrigger = true;
        entry.offset = new Vector2(-0.04f, -0.28f);
        var controller = go.AddComponent<TeleportController>(); var so = new SerializedObject(controller);
        so.FindProperty("closedVisual").objectReferenceValue = closed;
        so.FindProperty("openVisual").objectReferenceValue = open;
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(go, "Hive");
    }
    static RuntimeAnimatorController MakeBeeAnimation()
    {
        const string folder = "Assets/BeeGlade/Animation/";
        var clip = new AnimationClip { name = "Bee_WingFlap", frameRate = 24 };
        var sprites = Sprites("bee-sheet.png");
        if (sprites.Length != 12) throw new Exception("Expected 12 bee frames, found " + sprites.Length);
        var keys = new ObjectReferenceKeyframe[13];
        for (int i = 0; i <= 12; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 24f, value = sprites[i % 12] };
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding {
            path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, folder + "Bee_WingFlap.anim");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "Bee.controller");
        var state = controller.layers[0].stateMachine.AddState("WingFlap"); state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        return controller;
    }
    static void ValidateScene()
    {
        if (UnityEngine.Object.FindObjectsOfType<Crystal>().Length != 3) throw new Exception("Expected 3 flowers");
        if (UnityEngine.Object.FindObjectsOfType<Hazard>().Length != 1) throw new Exception("Expected 1 web");
        if (UnityEngine.Object.FindObjectOfType<TeleportController>() == null) throw new Exception("Hive missing");
        foreach (var script in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            if (script == null) throw new Exception("Missing scene script");
    }

    public static void UpdateHiveTrigger()
    {
        var path = Prefabs + "Hive.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.GetComponent<BoxCollider2D>().isTrigger = true;
            var trunk = root.transform.Find("SolidUpperTrunk");
            if (trunk != null) UnityEngine.Object.DestroyImmediate(trunk.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var hive in UnityEngine.Object.FindObjectsOfType<TeleportController>())
        {
            hive.GetComponent<Collider2D>().isTrigger = true;
            var trunk = hive.transform.Find("SolidUpperTrunk");
            if (trunk != null) UnityEngine.Object.DestroyImmediate(trunk.gameObject);
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BEEGLADE_HIVE_TRIGGER_UPDATED");
    }

    [MenuItem("Bee Glade/Build Windows game")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Builds/Windows/BeeGlade.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        File.WriteAllText("build-report.json", "{\"result\":\"" + report.summary.result +
            "\",\"errors\":" + report.summary.totalErrors + "}");
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed");
    }
}
