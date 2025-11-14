using UnityEngine;

public class State_Copying : IInputState
{
    // --- Инструменты ---
    private readonly PlayerInputController _controller;
    private readonly NotificationManager _notificationManager;
    private readonly BuildingManager _buildingManager;
    private readonly GridSystem _gridSystem; // <-- ИСПРАВЛЕНИЕ 1
    private readonly SelectionManager _selectionManager;
    private readonly GroupOperationHandler _groupOperationHandler;
    
    private bool _isDragging = false;
    private Vector2Int _dragStartPosition;

    // --- ИСПРАВЛЕНИЕ 2 ---
    public State_Copying(PlayerInputController controller, NotificationManager notificationManager, 
                         BuildingManager buildingManager, GridSystem gridSystem, 
                         SelectionManager selectionManager, GroupOperationHandler groupOperationHandler)
    {
        _controller = controller;
        _notificationManager = notificationManager;
        _buildingManager = buildingManager;
        _gridSystem = gridSystem; // <-- И присвоили
        _selectionManager = selectionManager;
        _groupOperationHandler = groupOperationHandler;
    }

    public void OnEnter()
    {
        _notificationManager.ShowNotification("Режим: Копирование (Пипетка)");
        _isDragging = false;
    }

    public void OnUpdate()
    {
        bool isOverUI = _controller.IsPointerOverUI();
        Vector2Int gridPos = GridSystem.MouseGridPosition;
        Vector3 worldPos = _controller.GetMouseWorldPosition();

        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            // --- ИСПРАВЛЕНИЕ 3 ---
            BuildingIdentity id = _gridSystem.GetBuildingIdentityAt(gridPos.x, gridPos.y);

            if (id != null)
            {
                // ... (остальной код не меняется)
                _buildingManager.TryCopyBuilding(gridPos);
            }
            else if (gridPos.x != -1)
            {
                _isDragging = true;
                _dragStartPosition = gridPos;
                _selectionManager.StartSelection(worldPos);
            }
        }

        if (Input.GetMouseButton(0) && _isDragging)
        {
            _selectionManager.UpdateSelection(worldPos);
        }

        if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;
            var selection = _selectionManager.FinishSelectionAndSelect(worldPos);
            if (selection.Count > 0)
            {
                _groupOperationHandler.StartMassCopy(selection);
                SelectionManager.Instance.ClearSelection();
            }
        }

        if (Input.GetMouseButtonDown(1) && !_isDragging)
        {
            _controller.SetMode(InputMode.None);
        }
    }

    public void OnExit()
    {
        // ВАЖНО: не зовём CancelAllModes(), иначе убьём призрака,
        // которого только что создаст EnterBuildMode().
        _selectionManager.HideSelectionVisuals();
        _isDragging = false;
    }
}