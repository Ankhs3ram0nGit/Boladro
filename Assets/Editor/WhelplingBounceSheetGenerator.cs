using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WhelplingBounceSheetGenerator
{
    private const string SourcePath = "Assets/Creatures/whelpling.png";
    private const string PreviewOutPath = "Assets/Creatures/whelpling_bounce_preview.png";
    private const string RuntimeOutPath = "Assets/Resources/Creatures/whelpling_bounce_runtime.png";
    private const int FrameCount = 7;

    [MenuItem("Tools/Whelpling/Regenerate Bounce Sheet")]
    public static void Generate()
    {
        Texture2D srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
        if (srcTex == null) return;

        EnsureFolder("Assets/Resources/Creatures");

        TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
        if (importer == null) return;

        bool restoreReadable = !importer.isReadable;
        if (restoreReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            srcTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
        }

        Sprite sourceSprite = LoadPrimarySprite(SourcePath);
        if (sourceSprite == null)
        {
            if (restoreReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            return;
        }

        Rect r = sourceSprite.rect;
        int sx = Mathf.RoundToInt(r.x);
        int sy = Mathf.RoundToInt(r.y);
        int sw = Mathf.RoundToInt(r.width);
        int sh = Mathf.RoundToInt(r.height);

        Color32[] srcPixels = srcTex.GetPixels32();
        int texW = srcTex.width;

        int baseline = FindBaseline(srcPixels, texW, sx, sy, sw, sh);
        if (baseline < 0) baseline = 0;

        // Scale-only deformation with fixed baseline (no bottom drift).
        float[] scaleY = new float[] { 1.00f, 0.92f, 1.04f, 1.12f, 1.04f, 0.90f, 0.98f };

        Texture2D preview = NewSheet(sw, sh, FrameCount, true);
        Texture2D runtime = NewSheet(sw, sh, FrameCount, false);

        for (int frame = 0; frame < FrameCount; frame++)
        {
            DrawFrame(srcPixels, texW, sx, sy, sw, sh, baseline, preview, frame, scaleY[frame], true);
            DrawFrame(srcPixels, texW, sx, sy, sw, sh, baseline, runtime, frame, scaleY[frame], false);
        }

        preview.filterMode = FilterMode.Point;
        runtime.filterMode = FilterMode.Point;
        preview.Apply();
        runtime.Apply();

        File.WriteAllBytes(PreviewOutPath, preview.EncodeToPNG());
        File.WriteAllBytes(RuntimeOutPath, runtime.EncodeToPNG());

        AssetDatabase.ImportAsset(PreviewOutPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(RuntimeOutPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureImporter(PreviewOutPath, false);
        ConfigureImporter(RuntimeOutPath, true);
        AssetDatabase.Refresh();

        if (restoreReadable)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    private static Sprite LoadPrimarySprite(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        return all.OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height)
            .FirstOrDefault();
    }

    private static int FindBaseline(Color32[] pixels, int texWidth, int sx, int sy, int sw, int sh)
    {
        int baseline = -1;
        for (int y = 0; y < sh; y++)
        {
            bool found = false;
            for (int x = 0; x < sw; x++)
            {
                int idx = (sy + y) * texWidth + (sx + x);
                if (pixels[idx].a > 0)
                {
                    baseline = y;
                    found = true;
                    break;
                }
            }
            if (found) break;
        }
        return baseline;
    }

    private static Texture2D NewSheet(int frameW, int frameH, int frames, bool blackBackground)
    {
        Texture2D tex = new Texture2D(frameW * frames, frameH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        Color32 bg = blackBackground ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
        Color32[] fill = Enumerable.Repeat(bg, tex.width * tex.height).ToArray();
        tex.SetPixels32(fill);
        return tex;
    }

    private static void DrawFrame(
        Color32[] srcPixels,
        int texW,
        int sx,
        int sy,
        int sw,
        int sh,
        int baseline,
        Texture2D dst,
        int frameIndex,
        float scaleY,
        bool blackBackground)
    {
        int frameX = frameIndex * sw;

        for (int y = 0; y < sh; y++)
        {
            for (int x = 0; x < sw; x++)
            {
                Color32 c = srcPixels[(sy + y) * texW + (sx + x)];
                if (c.a == 0) continue;

                int relY = y - baseline;
                int scaledRelY = Mathf.RoundToInt(relY * scaleY);
                int newY = baseline + scaledRelY;
                if (newY < 0 || newY >= sh) continue;

                int dstX = frameX + x;
                int dstY = newY;

                if (blackBackground)
                {
                    // Keep original pixel color/details exactly.
                    dst.SetPixel(dstX, dstY, c);
                }
                else
                {
                    dst.SetPixel(dstX, dstY, c);
                }
            }
        }
    }

    private static void ConfigureImporter(string path, bool runtimeTransparent)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 128;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = runtimeTransparent;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }
}
