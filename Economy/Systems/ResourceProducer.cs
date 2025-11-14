// --- ResourceProducer.cs ---
using UnityEngine;

/// <summary>
/// Производит ресурсы по "циклам" (напр., 1 доска за 30 сек).
/// </summary>
public class ResourceProducer : MonoBehaviour
{
    [Tooltip("Данные о 'рецепте' (время, затраты, выход)")]
    public ResourceProductionData productionData;
    
    private BuildingInputInventory _inputInv;
    private BuildingOutputInventory _outputInv;
    
    [Header("Бонусы от Модулей")]
    [Tooltip("Производительность = База * (1.0 + (Кол-во модулей * X))")]
    public float productionPerModule = 0.25f;

    private float _currentModuleBonus = 1.0f; // (Множитель, 1.0 = 100%)
    
    [Header("Эффективность")]
    private float _efficiencyModifier = 1.0f; // 100% по дефолту
    
    // --- ⬇️ НОВЫЙ ТАЙМЕР ⬇️ ---
    [Header("Состояние цикла")]
    [SerializeField]
    [Tooltip("Внутренний таймер. Накапливается до 'cycleTimeSeconds'")]
    private float _cycleTimer = 0f;
    // --- ⬆️ КОНЕЦ НОВОГО ТАЙМЕРА ⬆️ ---
    
    public bool IsPaused { get; private set; } = false;

    void Awake()
    {
        _inputInv = GetComponent<BuildingInputInventory>();
        _outputInv = GetComponent<BuildingOutputInventory>();

        if (_inputInv == null && productionData != null && productionData.inputCosts.Count > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingInputInventory', но рецепт требует сырье!", this);
            
        if (_outputInv == null && productionData != null && productionData.outputYield.amount > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingOutputInventory', но рецепт производит товар!", this);
        
        if (_outputInv != null)
        {
            _outputInv.OnFull += PauseProduction;
            _outputInv.OnSpaceAvailable += ResumeProduction;
        }
    }
    
    private void OnDestroy()
    {
        if (_outputInv != null)
        {
            _outputInv.OnFull -= PauseProduction;
            _outputInv.OnSpaceAvailable -= ResumeProduction;
        }
    }

    // --- ⬇️ ПОЛНОСТЬЮ НОВЫЙ UPDATE ⬇️ ---
    void Update()
    {
        if (IsPaused || productionData == null)
            return;

        // 1. Считаем время цикла
        // (Учитываем все бонусы: Модули * Эффективность)
        float currentCycleTime = productionData.cycleTimeSeconds / (_currentModuleBonus * _efficiencyModifier);
        
        // 2. Накапливаем таймер
        _cycleTimer += Time.deltaTime;

        // 3. Ждем, пока таймер "дозреет"
        if (_cycleTimer < currentCycleTime)
        {
            return; // Еще не время
        }
        
        // --- 4. ВРЕМЯ ПРИШЛО! (Таймер сработал) ---
        _cycleTimer -= currentCycleTime; // Сбрасываем таймер (с учетом "сдачи")

        // 5. Проверяем "Желудок" (Input)
        // (null-check на _inputInv, т.к. Лесопилка его не имеет)
        if (_inputInv != null && !_inputInv.HasResources(productionData.inputCosts))
        {
            // Debug.Log($"[Producer] {gameObject.name} не хватает сырья.");
            return; // Нет сырья, ждем следующего цикла
        }

        // 6. Проверяем "Кошелек" (Output)
        // (null-check, если здание ничего не производит)
        if (_outputInv != null && !_outputInv.HasSpace(productionData.outputYield.amount))
        {
            PauseProduction(); // Склад полон
            return;
        }
        
        // --- 7. ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ! ПРОИЗВОДИМ! ---
        
        // а) "Съедаем" сырье
        if (_inputInv != null)
        {
            _inputInv.ConsumeResources(productionData.inputCosts);
        }
        
        // б) "Производим" товар
        if (_outputInv != null)
        {
            _outputInv.AddResource(productionData.outputYield.amount);
        }
    }
    // --- ⬆️ КОНЕЦ НОВОГО UPDATE ⬆️ ---


    /// <summary>
    /// Вызывается из ModularBuilding, когда кол-во модулей меняется.
    /// </summary>
    public void UpdateProductionRate(int moduleCount)
    {
        _currentModuleBonus = 1.0f + (moduleCount * productionPerModule);
        Debug.Log($"[Producer] {gameObject.name} обновил бонус. Модулей: {moduleCount}, Множитель: {_currentModuleBonus}x");
    }
    
    public void SetEfficiency(float normalizedValue)
    {
        _efficiencyModifier = normalizedValue;
    }
    public float GetEfficiency() => _efficiencyModifier;
    
    
    private void PauseProduction()
    {
        if (IsPaused) return;
        IsPaused = true;
        // Debug.Log($"Производство {gameObject.name} на ПАУЗЕ (склад полон).");
    }

    private void ResumeProduction()
    {
        if (!IsPaused) return;
        IsPaused = false;
        // Debug.Log($"Производство {gameObject.name} ВОЗОБНОВЛЕНО (место появилось).");
    }
}