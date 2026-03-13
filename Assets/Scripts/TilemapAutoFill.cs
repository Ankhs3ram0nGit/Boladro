using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class TilemapAutoFill : MonoBehaviour
{
    public TileBase tile;
    public int width = 20;
    public int height = 12;

    void OnEnable()
    {
        Apply();
    }

    public void Apply()
    {
        if (tile == null) return;

        Tilemap map = GetComponent<Tilemap>();
        map.ClearAllTiles();

        int startX = -width / 2;
        int startY = -height / 2;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                map.SetTile(new Vector3Int(startX + x, startY + y, 0), tile);
            }
        }
    }
}
