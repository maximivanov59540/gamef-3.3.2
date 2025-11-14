using UnityEngine;

/// <summary>
/// Режим "Улучшение" (из Проекта в Реальное)
/// </summary>
public class State_Upgrading : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly GridSystem _gridSystem;
    private readonly SelectionManager _selectionManager;
    
    // --- Память ---
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    public State_Upgrading(PlayerInputController controller, NotificationManager notificationManager, 
                           BuildingManager buildingManager, GridSystem gridSystem, SelectionManager selectionManager)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _gridSystem = gridSystem;
        _selectionManager = selectionManager;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Улучшение (Инструмент)");
        _isDragging = false;
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleUpgradingMode ---

        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();

        // 1. "НАЧАЛО" (Клик)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);
            if (id != null)
            {
                // --- СЛУЧАЙ А: "ПОШТУЧНЫЙ" КЛИК ---
                _buildingManager.TryUpgradeBuilding(gridPos);
            }
            else if (gridPos.x != -1)
            {
                // --- СЛУЧАЙ Б: "МАССОВЫЙ" ДРАГ ---
                _isDragging = true;
                _dragStartPosition = gridPos;
                _selectionManager.StartSelection(worldPos);
            }
        }

        // 2. "ПЛАНИРОВАНИЕ" (Драг)
        if (Input.GetMouseButton(0) && _isDragging)
        {
            _selectionManager.UpdateSelection(worldPos);
        }

        // 3. "ИСПОЛНЕНИЕ" (Отпускание Драга)
        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;
            var selection = _selectionManager.FinishSelectionAndSelect(worldPos);

            if (selection.Count > 0)
            {
                _buildingManager.MassUpgrade(selection);
            }
            // (При промахе - ничего не делаем, остаемся в режиме)
        }

        // 4. "Отмена" (ПКМ) - ВЫХОД из режима
        if (Input.GetMouseButtonDown(1) && !_isDragging)
        {
            _controller.SetMode(InputMode.None);
        }
    }

    public void OnExit()
    {
        // Тут тоже ничего не удаляем в BuildingManager.
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}