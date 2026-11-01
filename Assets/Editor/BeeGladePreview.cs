using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BeeGladePreview
{
    public static void CaptureUi(string path)
    {
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        var converted = new List<Canvas>();
        var distances = new List<float>();
        var orders = new List<int>();
        var overrides = new List<bool>();
        var target = new RenderTexture(1600, 900, 24);
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1600f / 900f;
            foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                converted.Add(canvas); distances.Add(canvas.planeDistance);
                orders.Add(canvas.sortingOrder); overrides.Add(canvas.overrideSorting);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32767;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < converted.Count; i++)
            {
                converted[i].renderMode = RenderMode.ScreenSpaceOverlay;
                converted[i].worldCamera = null;
                converted[i].planeDistance = distances[i];
                converted[i].sortingOrder = orders[i];
                converted[i].overrideSorting = overrides[i];
            }
            camera.targetTexture = previousTarget; camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            Canvas.ForceUpdateCanvases();
            Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
        }
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeeGlade.unity");
        var camera = Camera.main;
        var target = new RenderTexture(1600, 900, 24);
        camera.targetTexture = target;
        camera.aspect = 1600f / 900f;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        pixels.Apply();
        Directory.CreateDirectory("Verification");
        File.WriteAllBytes("Verification/scene-preview.png", pixels.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(pixels);
        Object.DestroyImmediate(target);
        Debug.Log("BEEGLADE_PREVIEW_OK");
    }
}
