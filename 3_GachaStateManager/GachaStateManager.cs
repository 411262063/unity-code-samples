using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UI;
using DG.Tweening;

public class GachaStateManager : MonoBehaviour
{
    public enum FlowState
    {
        ShowingStory,
        Idling,
        Transitioning,
        WaitingInput,
        Revealing,
        ShowingItems
    }

    [Header("影片")]
    public VideoPlayer videoPlayer;
    public VideoClip idleClip;
    [Tooltip("轉場動畫(等待輸入的手")]
    public VideoClip transitionClip;
    [Tooltip("揭示動畫(普通)")]
    public VideoClip normalClip;
    [Tooltip("揭示動畫(稀有)")]
    public VideoClip rareClip;

    [Range(0f,5f), Tooltip("延遲n秒 出現輸入提示")]
    public float delayForWaitInput = 1f;

    [Range(0f, 5f), Tooltip("延遲n秒 播放揭示音效")]
    public float delayForRevealSound = 1f;

    [Range(0f, 5f), Tooltip("延遲n秒 顯示獲得物品")]
    public float delayForShowItems = 2f;

    [Header("動畫")]
    public Animator animator;
  
    [Header("輸入")]
    public InputActionReference confirmRef; //在初始
    public HoldToConfirmCtr holdConfirmCtr; //從轉場>揭示的長按輸入
    public InputActionReference closeRef;   //在公會時可關閉回到城鎮

    [Header("UI")]
    public InitialGachaUICtr initialGachaUI;
    public GuildUICtr guildUI;
    public GetNewItemsUICtr getNewItemUI;
    public GetNewHeroUICtr getNewHeroUI;

    [Header("音效")]
    public AudioSource source;

    [Header("流程"), SerializeField]
    private FlowState currentFlowState;

    private GachaResultContainer currentResults;
    private VideoClip currentRevealClip;
    
    /// <summary>
    /// 是否為初始抽卡流程
    /// </summary>
    private bool isInitialGachaFlow;

    private void Awake()
    {
        Time.timeScale = 1f;
        PlayVideo(idleClip);
    }

    private void OnEnable()
    {
        GachaManager.OnDrawQuestCompleted += OnDrawCompleted;
        PlayVideo(idleClip);
    }

    private void OnDisable()
    {
        GachaManager.OnDrawQuestCompleted -= OnDrawCompleted;
        UnbindInputAction();
    }

    #region 初始化
    private void HideAllUI()
    {
        initialGachaUI.gameObject.SetActive(false);
        guildUI.gameObject.SetActive(false);
        getNewItemUI.gameObject.SetActive(false);
        getNewHeroUI.gameObject.SetActive(false);
    }

    private void ResetToIdle()
    {
        currentFlowState = FlowState.Idling;
        currentResults = null;
        currentRevealClip = null;
        DisableProceedInput();
        PlayVideo(idleClip);
        animator.Play("Showing");
    }

    private IEnumerator Start()
    {
        HideAllUI();
        ResetToIdle();
        yield return null;

        isInitialGachaFlow = GachaManager.Instance.IsInitialGacha;

        if (isInitialGachaFlow)
        {
            currentFlowState = FlowState.ShowingStory;
            initialGachaUI.OpenUI();
        }
        else
        {
            guildUI?.gameObject.SetActive(true);
        }

        BindInputAction();
    }
    #endregion

    #region 輸入綁定
    private void BindInputAction()
    {
        if (confirmRef != null)
        {
            confirmRef.action.started += OnConfirm;
            confirmRef.action.Enable();
        }

        if(closeRef != null)
        {
            closeRef.action.started += OnClose;
            closeRef.action.Enable();
        }
    }

    private void UnbindInputAction()
    {
        if (confirmRef != null)
        {
            confirmRef.action.started -= OnConfirm;
            confirmRef.action.Disable();
        }

        if(closeRef != null)
        {
            closeRef.action.started -= OnClose;
            closeRef.action.Disable();
        }
    }
    #endregion

    #region 輸入行為
    private bool CanConfirm()
    {
        if (currentFlowState != FlowState.ShowingItems) return false;
        if (!getNewItemUI.isActiveAndEnabled) return false;
        if (getNewHeroUI.isActiveAndEnabled) return false;
        if (getNewItemUI.IsProcessing) return false;
        return GameHandler.Instance.CanCloseCurrentUI();
    }

    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!CanConfirm()) return;

        if (isInitialGachaFlow)
        {
            GachaManager.Instance?.SetInitialGachaFlag(false);
            SceneTransitionManager.Instance?.ToScene("TownScene");
        }
        else
        {
            ReturnToGuild();
        }
    }

    private bool CanClose()
    {
        if(currentFlowState != FlowState.Idling) return false;
        if(getNewItemUI.isActiveAndEnabled) return false;
        if(getNewHeroUI.isActiveAndEnabled) return false;
        return GameHandler.Instance.CanCloseCurrentUI();
    }

    private void OnClose(InputAction.CallbackContext context)
    {
        if(!CanClose()) return;
        SceneTransitionManager.Instance.ToScene("TownScene");
    }
    #endregion

    #region 影片
    private void PlayVideo(VideoClip clip)
    {
        if (videoPlayer == null || clip == null) return;
        if (videoPlayer.clip == clip) return;
        videoPlayer.playbackSpeed = 1f;
        videoPlayer.isLooping = (clip == idleClip);
        videoPlayer.clip = clip;
        videoPlayer.Play();
    }
    #endregion

    #region 流程控制
    /// <summary>
    /// 抽卡完成 > 設定結果動畫 > 開始轉場
    /// </summary>
    /// <param name="results"></param>
    private void OnDrawCompleted(GachaResultContainer results)
    {
        currentResults = results;
        currentRevealClip = results.HasHero ? rareClip : normalClip;

        if (isInitialGachaFlow)
        {
            initialGachaUI.gameObject.SetActive(false);
        }
        else
        {
            guildUI.gameObject.SetActive(false);
        }

        StartTransitioning();
    }

    /// <summary>
    /// 轉場：設定轉場狀態、播放轉場動畫
    /// </summary>
    private void StartTransitioning()
    {
        currentFlowState = FlowState.Transitioning;
        animator?.Play("Hiding");
        PlayVideo(transitionClip);
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        yield return new WaitForSeconds(delayForWaitInput);
        StartWaitingInput();
    }

    /// <summary>
    /// 等待輸入 (轉場 > 結果)
    /// </summary>
    private void StartWaitingInput()
    {
        currentFlowState = FlowState.WaitingInput;
        EnableProceedInput();
    }

    /// <summary>
    /// 揭示：設定揭示狀態、播放結果動畫
    /// </summary>
    private void StartRevealing()
    {
        if (currentFlowState != FlowState.WaitingInput) return;
        DisableProceedInput();
        currentFlowState = FlowState.Revealing;
        PlayVideo(currentRevealClip);
        if (source) source.PlayDelayed(delayForRevealSound);
        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        yield return new WaitForSeconds(delayForShowItems);
        StartShowingItems();
    }

    /// <summary>
    /// 顯示獲得物品
    /// </summary>
    private void StartShowingItems()
    {
        currentFlowState = FlowState.ShowingItems;
        getNewItemUI.ShowGachaItems(currentResults, getNewHeroUI);
    }
    #endregion

    #region 啟用長按輸入/提示
    private void EnableProceedInput()
    {
        animator?.Play("ShowHint", 1);
        holdConfirmCtr?.Activate(StartRevealing);
    }

    private void DisableProceedInput()
    {
        animator?.Play("HideHint", 1);
        holdConfirmCtr?.Deactivate();
    }
    #endregion

    private void ReturnToGuild()
    {
        getNewItemUI.gameObject.SetActive(false);
        getNewHeroUI.gameObject.SetActive(false);
        guildUI.gameObject.SetActive(true);
        ResetToIdle();
    }
}