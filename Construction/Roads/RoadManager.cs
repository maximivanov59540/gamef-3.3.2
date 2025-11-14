using UnityEngine;
using System.Collections.Generic;

public class RoadManager : MonoBehaviour
{
    public static RoadManager Instance { get; private set; }
    public event System.Action<Vector2Int> OnRoadAdded;
    public event System.Action<Vector2Int> OnRoadRemoved;


    [Header("Ссылки на 'Инструменты'")]
    [SerializeField] private GridSystem gridSystem;

    [Header("Префаб Дороги")]
    [SerializeField] private GameObject roadPrefab; 
    [SerializeField] private Transform roadsRoot;

    // ── НОВОЕ: граф дорог (смежные списки) ─────────────────────
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _roadGraph = new Dictionary<Vector2Int, List<Vector2Int>>();

    private static readonly Vector2Int[] DIRS = new[]
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (gridSystem == null)
        {
            gridSystem = FindFirstObjectByType<GridSystem>();
        }

        if (roadPrefab == null)
        {
            Debug.LogError("RoadManager: 'Road Prefab' не назначен!", this);
        }
        if (roadsRoot == null)
        {
            var go = GameObject.Find("RoadsRoot");
            if (go == null) go = new GameObject("RoadsRoot");
            roadsRoot = go.transform;
        }
        roadsRoot.SetParent(null);
        roadsRoot.position = Vector3.zero;

        // ВОССТАНОВИМ граф из уже существующих в сцене тайлов:
        RebuildGraphFromScene();
    }


    public void PlaceRoad(Vector2Int gridPos)
    {
        if (gridPos.x == -1) return;

        // ЖЁСТКАЯ защита
        if (gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y) != null) return;
        if (gridSystem.GetRoadTileAt(gridPos.x, gridPos.y) != null) return;
        if (gridSystem.IsCellOccupied(gridPos.x, gridPos.y)) return;

        // Создаем физику
        Vector3 worldPos = gridSystem.GetWorldPosition(gridPos.x, gridPos.y);
        float offset = gridSystem.GetCellSize() / 2f;
        worldPos.x += offset;
        worldPos.z += offset;
        worldPos.y += 0.01f;

        GameObject roadGO = Instantiate(roadPrefab, worldPos, Quaternion.Euler(90, 0, 0), roadsRoot);
        roadGO.name = $"Road_{gridPos.x}_{gridPos.y}";

        if (Physics.Raycast(worldPos + Vector3.up * 50f, Vector3.down, out var hit, 200f, 1 << LayerMask.NameToLayer("Ground")))
        {
            worldPos.y = hit.point.y + 0.01f;
            roadGO.transform.position = worldPos;
        }
        else
        {
            worldPos.y = gridSystem.GetWorldPosition(0, 0).y + 0.01f;
            roadGO.transform.position = worldPos;
        }

        RoadTile roadTileComponent = roadGO.GetComponent<RoadTile>();
        if (roadTileComponent == null)
        {
            Debug.LogError($"Префаб дороги ({roadPrefab.name}) не содержит 'RoadTile'!", roadGO);
            Destroy(roadGO);
            return;
        }

        // Регистрируем в GridSystem
        gridSystem.SetRoadTile(gridPos, roadTileComponent);

        // ── НОВОЕ: обновляем граф ───────────────────────────────
        if (!_roadGraph.ContainsKey(gridPos))
            _roadGraph[gridPos] = new List<Vector2Int>(4);

        foreach (var d in DIRS)
        {
            Vector2Int nb = gridPos + d;
            var nbTile = gridSystem.GetRoadTileAt(nb.x, nb.y);
            if (nbTile == null) continue;

            if (!_roadGraph.ContainsKey(nb))
                _roadGraph[nb] = new List<Vector2Int>(4);

            // дружим двусторонне (без дублей)
            if (!_roadGraph[gridPos].Contains(nb))
                _roadGraph[gridPos].Add(nb);
            if (!_roadGraph[nb].Contains(gridPos))
                _roadGraph[nb].Add(gridPos);
        }
        OnRoadAdded?.Invoke(gridPos);
    }

    public void RemoveRoad(Vector2Int gridPos)
    {
        if (gridPos.x == -1) return;

        RoadTile roadTileComponent = gridSystem.GetRoadTileAt(gridPos.x, gridPos.y);
        if (roadTileComponent == null) return;

        OnRoadRemoved?.Invoke(gridPos);

        // Разрываем связи в графе
        if (_roadGraph.TryGetValue(gridPos, out var neighbours))
        {
            var copy = ListPool<Vector2Int>.Get();
            copy.AddRange(neighbours);
            foreach (var nb in copy)
                if (_roadGraph.TryGetValue(nb, out var list))
                    list.Remove(gridPos);
            _roadGraph.Remove(gridPos);
            ListPool<Vector2Int>.Release(copy);
        }

        // Удаляем физику/сетку
        gridSystem.SetRoadTile(gridPos, null);
        Destroy(roadTileComponent.gameObject);
    }

    // ── НОВОЕ: публичный доступ к графу ───────────────────────
    public Dictionary<Vector2Int, List<Vector2Int>> GetRoadGraph() => _roadGraph;
    private void RebuildGraphFromScene()
    {
        _roadGraph.Clear();

        IEnumerable<RoadTile> tiles;
        if (roadsRoot != null)
        {
            // true — берём даже неактивные (вдруг у тебя есть скрытые сегменты дорог)
            tiles = roadsRoot.GetComponentsInChildren<RoadTile>(true);
        }
        else
        {
            // Фолбэк на глобальный поиск (см. Вариант 2)
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            tiles = UnityEngine.Object.FindObjectsByType<RoadTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
tiles = UnityEngine.Object.FindObjectsOfType<RoadTile>(includeInactive: true);
#endif

        }

        foreach (var tile in tiles)
        {
            // Определим клетку по позиции мира
            int gx, gz;
            gridSystem.GetXZ(tile.transform.position, out gx, out gz);
            var pos = new Vector2Int(gx, gz);

            // Убедимся, что GridSystem тоже знает про этот тайл
            if (gridSystem.GetRoadTileAt(pos.x, pos.y) != tile)
                gridSystem.SetRoadTile(pos, tile);

            if (!_roadGraph.ContainsKey(pos))
                _roadGraph[pos] = new List<Vector2Int>(4);
        }

        // Подружим соседей (4-направления), как это делается в PlaceRoad(...)
        foreach (var kv in _roadGraph)
        {
            var pos = kv.Key;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var nb = pos + d;
                var nbTile = gridSystem.GetRoadTileAt(nb.x, nb.y);
                if (nbTile == null) continue;

                if (!_roadGraph.ContainsKey(nb))
                    _roadGraph[nb] = new List<Vector2Int>(4);

                if (!_roadGraph[pos].Contains(nb))
                    _roadGraph[pos].Add(nb);
                if (!_roadGraph[nb].Contains(pos))
                    _roadGraph[nb].Add(pos);
            }
        }

        // Дадим знать слушателям, что граф появился
        foreach (var pos in _roadGraph.Keys)
            OnRoadAdded?.Invoke(pos);
    }
}

/// Очень простой пул для временных списков, чтобы не аллоцировать лишнее.
static class ListPool<T>
{
    private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

    public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>();
    public static void Release(List<T> list)
    {
        list.Clear();
        Pool.Push(list);
    }
    // ВОССТАНОВЛЕНИЕ ГРАФА ДЛЯ ПРЕДРАЗМЕЩЕННЫХ ДОРОГ
}
