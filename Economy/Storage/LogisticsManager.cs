using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class LogisticsManager : MonoBehaviour
{
    public static LogisticsManager Instance { get; private set; }

    // --- Ссылки на системы ---
    private GridSystem _gridSystem;
    private RoadManager _roadManager;
    
    // --- "Доска Заказов" ---
    private readonly List<ResourceRequest> _activeRequests = new List<ResourceRequest>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // "Хватаем" системы, нужные для поиска пути
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance;
    }

    /// <summary>
    /// Здание-потребитель (InputInventory) "вешает" свой заказ на доску.
    /// </summary>
    public void CreateRequest(ResourceRequest request)
    {
        if (!_activeRequests.Contains(request))
        {
            _activeRequests.Add(request);
            Debug.Log($"[LogisticsManager] Новый запрос на {request.RequestedType} от {request.Requester.name} (Приоритет: {request.Priority})");
        }
    }

    /// <summary>
    /// Здание-потребитель (InputInventory) "снимает" свой заказ (т.к. склад полон).
    /// </summary>
    public void FulfillRequest(ResourceRequest request)
    {
        if (_activeRequests.Contains(request))
        {
            _activeRequests.Remove(request);
            Debug.Log($"[LogisticsManager] Запрос на {request.RequestedType} от {request.Requester.name} выполнен/отменен.");
        }
    }
    public ResourceRequest GetBestRequest(Vector2Int cartGridPos, ResourceType resourceToDeliver, float roadRadius)
    {
        if (_activeRequests.Count == 0 || _roadManager == null || _gridSystem == null)
        {
            return null; // Нет запросов или системы не готовы
        }

        var roadGraph = _roadManager.GetRoadGraph();
        if (roadGraph == null || roadGraph.Count == 0) return null;

        // 1. Находим "вход" тележки на дорогу
        Vector2Int cartRoadCell = FindNearestRoadAccess(cartGridPos, roadGraph);
        if (cartRoadCell.x == -1)
        {
            return null; // Тележка сама не у дороги?
        }
            
        // 2. Фильтруем запросы по НУЖНОМУ ТИПУ ресурса
        var matchingRequests = _activeRequests.Where(r => r.RequestedType == resourceToDeliver).ToList();
        if (matchingRequests.Count == 0)
        {
            return null; // Никто не ждет этот ресурс
        }

        // 3. Считаем расстояния от тележки до ВСЕХ достижимых точек
        // (Мы ограничиваем поиск радиусом, чтобы не считать всю карту)
        int maxSteps = Mathf.FloorToInt(roadRadius);
        var distancesFromCart = LogisticsPathfinder.Distances_BFS(cartRoadCell, maxSteps, roadGraph);

        // 4. Собираем список "валидных" запросов (до которых можно доехать)
        var validRequests = new List<(ResourceRequest request, int distance)>();

        foreach (var req in matchingRequests)
        {
            // Находим "вход" на дорогу для "заказчика"
            Vector2Int destRoadCell = FindNearestRoadAccess(req.DestinationCell, roadGraph);
            if (destRoadCell.x == -1) continue; // Заказчик не у дороги

            // Проверяем, "досчитал" ли наш BFS до этой точки
            if (distancesFromCart.TryGetValue(destRoadCell, out int dist))
            {
                // Успех! Добавляем в список
                validRequests.Add((req, dist));
            }
        }

        // 5. Сортируем: Сначала по Приоритету (убывание), потом по Дистанции (возрастание)
        var sortedRequests = validRequests
            .OrderByDescending(r => r.request.Priority)
            .ThenBy(r => r.distance);

        // 6. Возвращаем "самый-самый"
        return sortedRequests.FirstOrDefault().request; // (Вернет null, если список пуст)
    }
    
    
    // --- ХЕЛПЕР ПОИСКА ДОРОГИ ---
    // (Этот код скопирован из CartAgent.cs, т.к. он нужен и здесь)
    private Vector2Int FindNearestRoadAccess(Vector2Int buildingCell, Dictionary<Vector2Int, List<Vector2Int>> graph)
    {
        if (graph.ContainsKey(buildingCell)) return buildingCell;
        
        BuildingIdentity identity = _gridSystem.GetBuildingIdentityAt(buildingCell.x, buildingCell.y);

        if (identity == null)
        {
            // Ищем в 4-х соседях
            Vector2Int[] neighbors = {
                buildingCell + Vector2Int.up,
                buildingCell + Vector2Int.down,
                buildingCell + Vector2Int.left,
                buildingCell + Vector2Int.right
            };
            foreach(var cell in neighbors)
            {
                if (graph.ContainsKey(cell)) return cell;
            }
            return new Vector2Int(-1, -1);
        }

        // Сканируем ВЕСЬ периметр здания
        Vector2Int root = identity.rootGridPosition;
        Vector2Int size = identity.buildingData.size;
        
        float yRotation = identity.yRotation; 
        if (Mathf.Abs(yRotation - 90f) < 1f || Mathf.Abs(yRotation - 270f) < 1f)
        {
            size = new Vector2Int(size.y, size.x);
        }

        int minX = root.x - 1; int maxX = root.x + size.x;
        int minZ = root.y - 1; int maxZ = root.y + size.y;

        for (int x = minX; x <= maxX; x++)
        {
            Vector2Int topCell = new Vector2Int(x, maxZ);
            if (graph.ContainsKey(topCell)) return topCell;
            Vector2Int bottomCell = new Vector2Int(x, minZ);
            if (graph.ContainsKey(bottomCell)) return bottomCell;
        }
        for (int z = minZ + 1; z < maxZ; z++)
        {
            Vector2Int leftCell = new Vector2Int(minX, z);
            if (graph.ContainsKey(leftCell)) return leftCell;
            Vector2Int rightCell = new Vector2Int(maxX, z);
            if (graph.ContainsKey(rightCell)) return rightCell;
        }

        return new Vector2Int(-1, -1);
    }
}