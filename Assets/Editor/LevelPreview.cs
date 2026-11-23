using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MineEmbers.EditorTools
{
    /// <summary>Renders every ShotPoint of Mine.unity from the editor (no player build) for quick art review.
    /// Static frame: no flicker, no runtime shadow budget (TierLightManager runs only in play mode).
    ///   tools/unity.sh MineEmbers.EditorTools.LevelPreview.Run   -> out/preview/*.png</summary>
    public static class LevelPreview
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Mine.unity");
            var dir = Path.GetFullPath("out/preview");
            Directory.CreateDirectory(dir);
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            foreach (var sp in Object.FindObjectsByType<MineEmbers.Boot.ShotPoint>(FindObjectsSortMode.None))
            {
                cam.transform.SetPositionAndRotation(sp.transform.position, sp.transform.rotation);
                cam.Render(); cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, sp.shotId + ".png"), tex.EncodeToPNG());
            }
            // Close-up of the player (character art / scale check). Sample the idle pose once in edit mode.
            var pc = Object.FindFirstObjectByType<MineEmbers.Gameplay.PlayerController>();
            if (pc)
            {
                if (pc.animator) { pc.animator.Rebind(); pc.animator.Update(0.5f); }
                var t = pc.transform;
                cam.transform.position = t.position + new Vector3(1.6f, 0.6f, 2.4f);
                cam.transform.LookAt(t.position + Vector3.up * 0.1f);
                cam.Render(); cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, "player.png"), tex.EncodeToPNG());
            }
            RenderTexture.active = null; cam.targetTexture = null;
            Debug.Log($"[LevelBuilder] preview written to {dir}");
        }
    }
}
