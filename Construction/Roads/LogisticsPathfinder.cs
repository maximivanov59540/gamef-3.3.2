using System.Collections.Generic;
using UnityEngine;

public static class LogisticsPathfinder
{
    /// Быстрый ответ «есть ли путь» (как было).
    public static bool HasPath_BFS(Vector2Int start, Vector2Int end,
        Dictionary<Vector2Int, List<Vector2Int>> graph)
    {
        if (start == end) return true;
        if (graph == null) return false;
        if (!graph.ContainsKey(start) || !graph.ContainsKey(end)) return false;

        var q = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        visited.Add(start);
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == end) return true;

            var neigh = graph[cur];
            for (int i = 0; i < neigh.Count; i++)
            {
                var nb = neigh[i];
                if (visited.Add(nb))
                    q.Enqueue(nb);
            }
        }
        return false;
    }

    /// КАРТА расстояний (в шагах по дорогам) от start до всех достижимых узлов, с отсечкой по maxSteps.
    public static Dictionary<Vector2Int, int> Distances_BFS(
        Vector2Int start,
        int maxSteps,
        Dictionary<Vector2Int, List<Vector2Int>> graph)
    {
        var dist = new Dictionary<Vector2Int, int>(256);
        if (graph == null || !graph.ContainsKey(start)) return dist;

        var q = new Queue<Vector2Int>();
        dist[start] = 0;
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int d = dist[cur];
            if (d >= maxSteps) continue;

            var neigh = graph[cur];
            for (int i = 0; i < neigh.Count; i++)
            {
                var nb = neigh[i];
                if (!dist.ContainsKey(nb))
                {
                    dist[nb] = d + 1;
                    q.Enqueue(nb);
                }
            }
        }
        return dist;
    }
    public static Dictionary<Vector2Int, int> Distances_BFS_Multi(
    IEnumerable<Vector2Int> starts,
    int maxSteps,
    Dictionary<Vector2Int, List<Vector2Int>> graph)
    {
        var dist = new Dictionary<Vector2Int, int>(256);
        if (graph == null) return dist;

        var q = new Queue<Vector2Int>();

        // добавить все валидные старты
        foreach (var s in starts)
        {
            if (!graph.ContainsKey(s)) continue;
            if (dist.ContainsKey(s)) continue;
            dist[s] = 0;
            q.Enqueue(s);
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int d = dist[cur];
            if (d >= maxSteps) continue;

            var neigh = graph[cur];
            for (int i = 0; i < neigh.Count; i++)
            {
                var nb = neigh[i];
                if (!dist.ContainsKey(nb))
                {
                    dist[nb] = d + 1;
                    q.Enqueue(nb);
                }
            }
        }
        return dist;
    }
    public static List<Vector2Int> FindActualPath(
        Vector2Int start, 
        Vector2Int end, 
        Dictionary<Vector2Int, List<Vector2Int>> graph)
    {
        if (start == end) return new List<Vector2Int> { start };
        if (graph == null) return null;
        if (!graph.ContainsKey(start) || !graph.ContainsKey(end)) return null;

        var q = new Queue<Vector2Int>();
        // "cameFrom" - это наш "клубок Ариадны".
        // Для каждой клетки (Key) мы записываем, из какой клетки (Value) мы в нее пришли.
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(); 

        q.Enqueue(start);
        cameFrom[start] = start; // Стартовая точка "пришла" сама из себя (маркер)

        bool found = false;

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == end)
            {
                found = true;
                break; // Путь найден!
            }

            var neigh = graph[cur];
            for (int i = 0; i < neigh.Count; i++)
            {
                var nb = neigh[i];
                if (!cameFrom.ContainsKey(nb)) // Если мы еще не были в этой клетке
                {
                    cameFrom[nb] = cur; // Запоминаем, что в 'nb' мы пришли из 'cur'
                    q.Enqueue(nb);
                }
            }
        }

        if (!found)
        {
            return null; // Путь не найден
        }

        // --- Восстановление пути ---
        // Теперь "разматываем" наш "клубок" cameFrom в обратном порядке.
        var path = new List<Vector2Int>();
        var current = end;

        while (current != start)
        {
            path.Add(current);
            current = cameFrom[current];
        }
        
        path.Add(start); // Добавляем саму стартовую точку
        path.Reverse(); // Переворачиваем, чтобы путь был от Start к End

        return path;
    }
}
