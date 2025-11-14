using UnityEngine;

/// <summary>
/// Режим "Перемещение" (Одиночное и Массовое)
/// </summary>
public class State_Moving : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly SelectionManager _selectionManager;
    private readonly GroupOperationHandler _groupOperationHandler;
    private readonly GridSystem _gridSystem;

    // --- Память ---
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    public State_Moving(PlayerInputController controller, NotificationManager notificationManager,
                        BuildingManager buildingManager, SelectionManager selectionManager, GroupOperationHandler groupOperationHandler, GridSystem gridSystem)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _selectionManager = selectionManager;
        _groupOperationHandler = groupOperationHandler;
        _gridSystem = gridSystem;
    }

    public void OnEnter()
    {
        _buildingManager?.ShowGrid(false); // <-- ДОБАВЬ ЭТУ СТРОКУ
    }

    public void OnUpdate()
    {
        // --- Это КОД из твоего старого HandleMovingMode ---

        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();
        
        // --- ЧАСТЬ 1: Мы УЖЕ "держим" 1 здание ---
        if (_buildingManager.IsHoldingBuilding())
        {
            _buildingManager.UpdateGhostPosition(gridPos, worldPos);

            if (Input.GetKeyDown(KeyCode.R))
            {
                _buildingManager.RotateGhost();
            }

            if (Input.GetMouseButtonDown(0) && !isOverUI)
            {
                _buildingManager.TryPlaceBuilding(gridPos);
                // (После успешной постановки, BuildingManager 
                //  сам сбросит _buildingToMove, и мы вернемся к "Части 2")
            }

            if (Input.GetMouseButtonDown(1))
            {
                // Отменяем только "перемещение", но остаемся в режиме
                _buildingManager.CancelAllModes(); 
            }
            return; // "Выходим", чтобы не "смешивать" логику
        }

        // --- ЧАСТЬ 2: Мы "свободны" и "ищем" цель ---

        // 1. "НАЧАЛО" (Клик)
        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

            if (id != null)
            {
                // --- СЛУЧАЙ А: "ПОШТУЧНЫЙ" КЛИК ---
                _buildingManager.TryPickUpBuilding(gridPos);
                // (Мы "остаемся" в режиме Moving, 
                //  но 'IsHoldingBuilding' в следующем кадре будет 'true')
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
                // "Приказываем" "Обработчику Групп" "начать ПЕРЕМЕЩЕНИЕ"
                _groupOperationHandler.StartMassMove(selection);
                SelectionManager.Instance.ClearSelection();
                // (GroupOperationHandler сам "переключит" режим в GroupMoving)
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
        // Не чистим BuildingManager тут — иначе снесём только что созданный призрак.
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}