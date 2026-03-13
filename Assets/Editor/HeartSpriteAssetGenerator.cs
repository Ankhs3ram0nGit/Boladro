using System.IO;
using UnityEditor;
using UnityEngine;

public static class HeartSpriteAssetGenerator
{
    const string Folder = "Assets/Resources/UI";
    const string FullPath = "Assets/Resources/UI/HeartFull.png";
    const string EmptyPath = "Assets/Resources/UI/HeartEmpty.png";

    [InitializeOnLoadMethod]
    static void EnsureHeartSprites()
    {
        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
        }

        Texture2D full = CreateBlockyHeartTexture(new Color32(220, 50, 60, 255), false);
        Texture2D empty = CreateBlockyHeartTexture(new Color32(255, 255, 255, 255), true);
        File.WriteAllBytes(FullPath, full.EncodeToPNG());
        File.WriteAllBytes(EmptyPath, empty.EncodeToPNG());

        AssetDatabase.ImportAsset(FullPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(EmptyPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureImporter(FullPath);
        ConfigureImporter(EmptyPath);
        AssetDatabase.Refresh();
    }

    static void ConfigureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static Texture2D CreateBlockyHeartTexture(Color32 color, bool outlineOnly)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        bool[,] mask = new bool[size, size]
        {
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,true ,true ,false,false,false,false,true ,true ,false,false,false,false},
            {false,false,false,true ,true ,true ,true ,false,false,true ,true ,true ,true ,false,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false,false},
            {false,false,false,false,true ,true ,true ,true ,true ,true ,true ,true ,false,false,false,false},
            {false,false,false,false,false,true ,true ,true ,true ,true ,true ,false,false,false,false,false},
            {false,false,false,false,false,false,true ,true ,true ,true ,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,true ,true ,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,true ,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false}
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool filled = mask[y, x];
                if (!filled)
                {
                    tex.SetPixel(x, size - 1 - y, new Color32(0, 0, 0, 0));
                    continue;
                }

                if (outlineOnly)
                {
                    bool edge = IsEdge(mask, x, y, size);
                    tex.SetPixel(x, size - 1 - y, edge ? color : new Color32(0, 0, 0, 0));
                }
                else
                {
                    tex.SetPixel(x, size - 1 - y, color);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    static bool IsEdge(bool[,] mask, int x, int y, int size)
    {
        if (!mask[y, x]) return false;

        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                int ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) return true;
                if (!mask[ny, nx]) return true;
            }
        }
        return false;
    }
}
