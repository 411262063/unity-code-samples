using Map;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance;

    /// <summary>
    /// 索引迭代事件 - 用於在列表或數組中進行索引導航
    /// <para>與 InputModule 的 Move 事件不同，此事件專門處理基於索引的迭代操作</para>
    /// <para>事件參數: +1 表示向前迭代，-1 表示向後迭代</para>
    /// </summary>
    /// <remarks>
    /// 此事件不會影響 UI InputModule 的四向選擇功能
    /// </remarks>
    public static event Action<int> LeftHand_Horizontal_IndexMove;
    public static event Action<int> RightHand_Horizontal_IndexMove;
    public static event Action<int> LeftHand_Vertical_IndexMove;
    public static event Action<int> RightHand_Vertical_IndexMove;

    /// <summary>
    /// 持續索引迭代事件 - 用於在列表或數組中進行連續的索引導航
    /// <para>當輸入持續按住時觸發，實現快速索引迭代</para>
    /// <para>事件參數: +1 表示向前迭代，-1 表示向後迭代</para>
    /// </summary>
    /// <remarks>
    /// 此事件不會影響 UI InputModule 的四向選擇功能
    /// </remarks>
    public static event Action<int> LeftHand_Horizontal_IndexMove_Continuous;
    public static event Action<int> RightHand_Horizontal_IndexMove_Continuous;
    public static event Action<int> LeftHand_Vertical_IndexMove_Continuous;
    public static event Action<int> RightHand_Vertical_IndexMove_Continuous;

    /// <summary>
    /// 索引迭代釋放事件 - 當按鍵放開時觸發
    /// <para>用於停止連續導航或其他需要在釋放時執行的操作</para>
    /// </summary>
    public static event Action LeftHand_Navigate_Released;
    public static event Action RightHand_Navigate_Released;

    [Header("基本輸入行為")]
    public InputActionReference navigate_left;
    public InputActionReference navigate_right;
    public InputActionReference holdingNavi_left;
    public InputActionReference holdingNavi_right;

    [Header("地圖輸入")]
    public InputActionReference toggleMapRef;
    public InputActionReference proceedFloorRef;

    [Header("暫停選單輸入")]
    public InputActionReference togglePauseRef;
    public InputActionReference exitPauseRef;

    //地圖輸入行為
    public static event Action OnToggleMapRequested;
    public static event Action OnProceedFloorRequested;
    //public static event Action OnGetBossHeroRequested;

    //暫停選單輸入行為
    public static event Action OnTogglePauseRequested;
    public static event Action OnExitPauseRequested;

    public bool isNaviActionsBinded {  get; private set; }

    public static event Action OnDeviceChanged;

    private string lastControlScheme = "";

    private void Awake()
    {
        isNaviActionsBinded = false;

        CloseMouseInput();

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindIndexIterationEvent();
        UnbindMapActions();
        UnbindPauseActions();
        EnableAllInputs(false);
    }

    private void Start()
    {
        BindIndexIterationEvent();
        BindMapActions();
        BindPauseActions();
        EnableAllInputs(true);
    }

    private void Update()
    {
        string currentScheme = Gamepad.current != null ? "Gamepad" : "Keyboard";

        if (currentScheme != lastControlScheme)
        {
            lastControlScheme = currentScheme;
            OnDeviceChanged?.Invoke();
        }
    }

    public bool IsUsingGamepad()
    {
        return Gamepad.current != null;
    }

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
    }

    private void CloseMouseInput()
    {
        Mouse.current?.MakeCurrent();
        InputSystem.DisableDevice(Mouse.current);

#if UNITY_EDITOR
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
#else
    // 發布模式：根據設定隱藏游標
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
#endif
    }

    #region 開啟/關閉所有輸入
    public void EnableAllInputs(bool on)
    {
        if (on)
        {
            EnableBasicActions();
            EnableMapActions();
            EnablePauseActions();
        }
        else
        {
            DisableBasicActions();
            DisableMapActions();
            DisablePauseActions();
        }
    }
    #endregion

    #region 導航輸入：綁定/解除
    private void BindIndexIterationEvent()
    {
        navigate_left.action.started += OnLeftNavigateStarted;
        navigate_right.action.started += OnRightNavigateStarted;

        holdingNavi_left.action.performed += OnLeftHoldPerformed;
        holdingNavi_right.action.performed += OnRightHoldPerformed;

        navigate_left.action.canceled += OnLeftNavigateCanceled;
        navigate_right.action.canceled += OnRightNavigateCanceled;
        holdingNavi_left.action.canceled += OnLeftNavigateCanceled;
        holdingNavi_right.action.canceled += OnRightNavigateCanceled;

        isNaviActionsBinded = true;
    }

    private void UnbindIndexIterationEvent()
    {
        navigate_left.action.started -= OnLeftNavigateStarted;
        navigate_right.action.started -= OnRightNavigateStarted;

        holdingNavi_left.action.performed -= OnLeftHoldPerformed;
        holdingNavi_right.action.performed -= OnRightHoldPerformed;

        navigate_left.action.canceled -= OnLeftNavigateCanceled;
        navigate_right.action.canceled -= OnRightNavigateCanceled;
        holdingNavi_left.action.canceled -= OnLeftNavigateCanceled;
        holdingNavi_right.action.canceled -= OnRightNavigateCanceled;
    }
    #endregion

    #region 導航輸入：開啟/關閉
    private void EnableBasicActions()
    {
        navigate_left.action.Enable();
        navigate_right.action.Enable();
        holdingNavi_left.action.Enable();
        holdingNavi_right.action.Enable();
    }

    private void DisableBasicActions()
    {
        navigate_left.action.Disable();
        navigate_right.action.Disable();
        holdingNavi_left.action.Disable();
        holdingNavi_right.action.Disable();
    }
    #endregion

    #region 導航輸入行為
    private void OnLeftNavigateStarted(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y)) //水平向
        {
            int xDirection = Mathf.RoundToInt(Mathf.Sign(value.x));
            LeftHand_Horizontal_IndexMove?.Invoke(xDirection);
        }
        else if (Mathf.Abs(value.y) > 0) //垂直向
        {
            int yDirection = Mathf.RoundToInt(Mathf.Sign(value.y));
            LeftHand_Vertical_IndexMove?.Invoke(yDirection);
        }
    }

    private void OnRightNavigateStarted(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y))
        {
            int xDirection = Mathf.RoundToInt(Mathf.Sign(value.x));
            RightHand_Horizontal_IndexMove?.Invoke(xDirection);
        }
        else if (Mathf.Abs(value.y) > 0)
        {
            int yDirection = Mathf.RoundToInt(Mathf.Sign(value.y));
            RightHand_Vertical_IndexMove?.Invoke(yDirection);
        }
    }

    private void OnLeftHoldPerformed(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y)) //水平向
        {
            int xDirection = Mathf.RoundToInt(Mathf.Sign(value.x));
            LeftHand_Horizontal_IndexMove_Continuous?.Invoke(xDirection);
        }
        else if (Mathf.Abs(value.y) > 0) //垂直向
        {
            int yDirection = Mathf.RoundToInt(Mathf.Sign(value.y));
            LeftHand_Vertical_IndexMove_Continuous?.Invoke(yDirection);
        }
    }

    private void OnRightHoldPerformed(InputAction.CallbackContext ctx)
    {
        Vector2 value = ctx.ReadValue<Vector2>();

        if (Mathf.Abs(value.x) > Mathf.Abs(value.y))
        {
            int xDirection = Mathf.RoundToInt(Mathf.Sign(value.x));
            RightHand_Horizontal_IndexMove_Continuous?.Invoke(xDirection);
        }
        else if (Mathf.Abs(value.y) > 0)
        {
            int yDirection = Mathf.RoundToInt(Mathf.Sign(value.y));
            RightHand_Vertical_IndexMove_Continuous?.Invoke(yDirection);
        }
    }

    private void OnLeftNavigateCanceled(InputAction.CallbackContext ctx)
    {
        LeftHand_Navigate_Released?.Invoke();
    }

    private void OnRightNavigateCanceled(InputAction.CallbackContext ctx)
    {
        RightHand_Navigate_Released?.Invoke();
    }
    #endregion

    #region 地圖
    private void EnableMapActions()
    {
        toggleMapRef.action.Enable();
        proceedFloorRef.action.Enable();
    }

    private void DisableMapActions()
    {
        toggleMapRef.action.Disable();
        proceedFloorRef.action.Disable();
    }

    private void BindMapActions()
    {
        if(toggleMapRef != null)
        {
            toggleMapRef.action.started += OnRequestToggleMap;
        }

        if(proceedFloorRef != null)
        {
            proceedFloorRef.action.started += OnRequestProceedFloor;
        }
    }

    private void UnbindMapActions()
    {
        if (toggleMapRef != null)
        {
            toggleMapRef.action.started -= OnRequestToggleMap;
        }

        if (proceedFloorRef != null)
        {
            proceedFloorRef.action.started -= OnRequestProceedFloor;
        }
    }

    private void OnRequestToggleMap(InputAction.CallbackContext context)
    {
        OnToggleMapRequested?.Invoke();
    }

    private void OnRequestProceedFloor(InputAction.CallbackContext context)
    {
        OnProceedFloorRequested?.Invoke();
    }
    #endregion

    #region 暫停選單
    private void EnablePauseActions()
    {
        togglePauseRef.action.Enable();
        exitPauseRef.action.Enable();
    }

    private void DisablePauseActions()
    {
        togglePauseRef.action.Disable();
        exitPauseRef.action.Disable();
    }

    private void BindPauseActions()
    {
        if(togglePauseRef != null)
        {
            togglePauseRef.action.started += OnRequestTogglePause;
        }

        if(exitPauseRef != null)
        {
            exitPauseRef.action.started += OnRequestExitPause;
        }
    }

    private void UnbindPauseActions()
    {
        if (togglePauseRef != null)
        {
            togglePauseRef.action.started -= OnRequestTogglePause;
        }

        if (exitPauseRef != null)
        {
            exitPauseRef.action.started -= OnRequestExitPause;
        }
    }

    private void OnRequestTogglePause(InputAction.CallbackContext context)
    {
        OnTogglePauseRequested?.Invoke();
    }

    private void OnRequestExitPause(InputAction.CallbackContext context)
    {
        OnExitPauseRequested?.Invoke();
    }
    #endregion
}
