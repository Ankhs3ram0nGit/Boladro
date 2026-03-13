using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class MapPainter : MonoBehaviour
{
    public int width = 40;
    public int height = 28;
    public int seed = 1;

    public TileBase grass;
    public TileBase flower;
    public TileBase path;
    public TileBase wall;

    void OnEnable()
    {
        Paint();
    }

    public void Paint()
    {
        if (grass == null) return;

        Tilemap map = GetComponent<Tilemap>();
        map.ClearAllTiles();

        int startX = -width / 2;
        int startY = -height / 2;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int gx = startX + x;
                int gy = startY + y;
                TileBase tileToSet = grass;

                bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                if (isBorder && wall != null)
                {
                    tileToSet = wall;
                }
                else if (path != null && (gx == 0 || gy == 0))
                {
                    tileToSet = path;
                }
                else if (flower != null)
                {
                    float n = Mathf.PerlinNoise((gx + seed) * 0.15f, (gy + seed) * 0.15f);
                    if (n > 0.75f)
                    {
                        tileToSet = flower;
                    }
                }

                map.SetTile(new Vector3Int(gx, gy, 0), tileToSet);
            }
        }
    }
}
