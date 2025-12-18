using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/// <summary>
/// Utility script to generate a rounded rectangle sprite for the ice rink surface.
/// This creates a proper ice hockey rink shape with straight sides and rounded ends.
/// </summary>
public class IceRinkSpriteGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Generate Ice Rink Sprite")]
    public static void GenerateIceRinkSprite()
    {
        // Sprite dimensions (will be scaled in scene)
        // International rink: 61m x 30m with 8.5m corner radius
        // Using 512x256 to maintain ~2:1 ratio
        int width = 512;  // Represents length (61m)
        int height = 256; // Represents width (30m)
        float cornerRadius = 56f; // Rounded corners (not full ends) - 8.5/30 * 256 ≈ 72, but using 56 for better look

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // Fill with transparent
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        // Draw rounded rectangle (ice rink shape)
        Color iceColor = new Color(1f, 1f, 1f, 1f); // White for ice

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (IsInsideRoundedRect(x, y, width, height, cornerRadius))
                {
                    pixels[y * width + x] = iceColor;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // Save as PNG
        byte[] pngData = texture.EncodeToPNG();
        string path = "Assets/Sprites/IceRinkSurface.png";

        // Create Sprites folder if it doesn't exist
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, pngData);

        // Refresh asset database
        AssetDatabase.Refresh();

        // Set texture import settings
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log($"Ice rink sprite generated at: {path}");

        // Select the created asset
        Object sprite = AssetDatabase.LoadAssetAtPath<Object>(path);
        Selection.activeObject = sprite;
    }

    private static bool IsInsideRoundedRect(float x, float y, float width, float height, float radius)
    {
        // Center coordinates
        float cx = width / 2f;
        float cy = height / 2f;

        // Translate to center
        float dx = Mathf.Abs(x - cx);
        float dy = Mathf.Abs(y - cy);

        // Dimensions of the straight sections
        float straightWidth = cx - radius;
        float straightHeight = cy - radius;

        // If in straight section
        if (dx <= straightWidth || dy <= straightHeight)
        {
            return dx <= cx && dy <= cy;
        }

        // Check if in rounded corner
        float cornerDx = dx - straightWidth;
        float cornerDy = dy - straightHeight;
        float distance = Mathf.Sqrt(cornerDx * cornerDx + cornerDy * cornerDy);

        return distance <= radius;
    }
#endif
}
