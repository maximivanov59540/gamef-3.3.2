using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [Header("Стартовые Лимиты")]
    [Tooltip("Начальный лимит для всех ресурсов (до постройки складов)")]
    public float baseResourceLimit = 100f;
    
    // БЫЛО: public Dictionary<ResourceType, float> resources
    // СТАЛО:
    public Dictionary<ResourceType, StorageData> GlobalStorage = new Dictionary<ResourceType, StorageData>();

    // --- Событие для UI ---
    // (UIResourceDisplay сможет подписаться на него, чтобы обновляться не каждый кадр)
    public event System.Action<ResourceType> OnResourceChanged;

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

        InitializeResources();
    }

    private void InitializeResources()
    {
        GlobalStorage.Clear();
        foreach (ResourceType resourceType in System.Enum.GetValues(typeof(ResourceType)))
        {
            // Создаем "слот" на складе с базовым лимитом
            GlobalStorage.Add(resourceType, new StorageData(0, baseResourceLimit));
        }

        // Выдаем стартовые ресурсы
        GlobalStorage[ResourceType.Wood].currentAmount = 100f;
        GlobalStorage[ResourceType.Stone].currentAmount = 50f;
        
        // Сразу "заполняем" лимит (если стартовые ресурсы > лимита)
        if (GlobalStorage[ResourceType.Wood].currentAmount > GlobalStorage[ResourceType.Wood].maxAmount)
             GlobalStorage[ResourceType.Wood].currentAmount = GlobalStorage[ResourceType.Wood].maxAmount;
             
        if (GlobalStorage[ResourceType.Stone].currentAmount > GlobalStorage[ResourceType.Stone].maxAmount)
             GlobalStorage[ResourceType.Stone].currentAmount = GlobalStorage[ResourceType.Stone].maxAmount;
    }

    /// <summary>
    /// Увеличивает (или уменьшает) ОБЩИЙ лимит хранения для ВСЕХ ресурсов.
    /// Вызывается складами (Warehouse).
    /// </summary>
    public void IncreaseGlobalLimit(float amount)
    {
        foreach (var slot in GlobalStorage.Values)
        {
            slot.maxAmount += amount;
            if (slot.maxAmount < 0) slot.maxAmount = 0; // Защита от отрицательного лимита
        }
        
        // (Можно добавить вызов общего события, чтобы UI обновил лимиты)
        OnResourceChanged?.Invoke(ResourceType.Wood); // Просто "пинаем" UI
    }

    /// <summary>
    /// Добавляет ресурс на Глобальный Склад (вызывается тележкой).
    /// </summary>
    /// <returns>Возвращает, сколько РЕАЛЬНО удалось добавить (с учетом лимита).</returns>
    public float AddToStorage(ResourceType type, float amount)
    {
        if (!GlobalStorage.ContainsKey(type)) return 0;

        StorageData slot = GlobalStorage[type];
        float spaceAvailable = slot.maxAmount - slot.currentAmount;

        if (spaceAvailable <= 0) return 0; // Склад полон

        float amountToAdd = Mathf.Min(amount, spaceAvailable);
        slot.currentAmount += amountToAdd;

        OnResourceChanged?.Invoke(type); // Уведомляем UI
        return amountToAdd;
    }

    /// <summary>
    /// Забирает ресурс с Глобального Склада (для тележек, везущих сырье).
    /// </summary>
    /// <returns>Возвращает, сколько РЕАЛЬНО удалось забрать.</returns>
    public float TakeFromStorage(ResourceType type, float amount)
    {
        if (!GlobalStorage.ContainsKey(type)) return 0;

        StorageData slot = GlobalStorage[type];

        if (slot.currentAmount <= 0) return 0; // Пусто

        float amountToTake = Mathf.Min(amount, slot.currentAmount);
        slot.currentAmount -= amountToTake;
        
        OnResourceChanged?.Invoke(type); // Уведомляем UI
        return amountToTake;
    }

    /// <summary>
    /// "Безопасное" списание для СТРОИТЕЛЬСТВА (использует int из BuildingData).
    /// </summary>
    public void SpendResources(ResourceType type, int amount)
    {
        // Просто вызываем наш новый float-метод
        TakeFromStorage(type, (float)amount);
    }
    
    // --- ОБНОВЛЕННЫЕ МЕТОДЫ (для совместимости) ---

    public float GetResourceAmount(ResourceType type)
    {
        if (GlobalStorage.ContainsKey(type))
        {
            return GlobalStorage[type].currentAmount;
        }
        return 0;
    }
    
    public float GetResourceLimit(ResourceType type)
    {
        if (GlobalStorage.ContainsKey(type))
        {
            return GlobalStorage[type].maxAmount;
        }
        return 0;
    }

    public bool CanAfford(BuildingData data)
    {
        if (data.costs == null) return true;

        foreach (var cost in data.costs)
        {
            // Проверяем по ТЕКУЩЕМУ количеству на складе
            if (GetResourceAmount(cost.resourceType) < cost.amount)
            {
                return false;
            }
        }
        return true;
    }

    public void SpendResources(BuildingData data)
    {
        if (data.costs == null) return;

        foreach (var cost in data.costs)
        {
            SpendResources(cost.resourceType, cost.amount);
        }
    }
    
    // (Старые методы CanAfford(type, float) и SpendResources(type, float)
    //  теперь дублируют TakeFromStorage, так что я их убрал для чистоты)
}