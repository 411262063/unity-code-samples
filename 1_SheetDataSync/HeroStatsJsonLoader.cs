using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;
using static HeroData;
using UnityEditor;
using System.Linq;
/// <summary>
/// https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Networking.UnityWebRequest.Get.html
/// </summary>

[Serializable]
public class HeroStatsJsonData
{
    public string pos;
    public List<int> maxHp_increment;
    public List<int> maxSp_increment;
    public List<int> pAtk_increment;
    public List<int> mAtk_increment;
    public List<int> agi_increment;
}

public class HeroStatsJsonLoader : BaseJsonLoader
{
    [Header("同步所有英雄的角色基本數值")]

    protected override string SheetName => "角色基本數值[程式]";
    protected override string ApiUrl => "HERO_SHEET_URL";

    //對應到試算表的各個欄位標題
    private const string ID_KEY = "heroId";
    private const string POS_KEY = "posType";
    private const string LEVEL_KEY = "lv";
    private const string MAX_HP_KEY = "maxHP";
    private const string MAX_SP_KEY = "maxSP";
    private const string MAX_TP_KEY = "maxTP";
    private const string P_ATK_KEY = "pAtk";
    private const string M_ATK_KEY = "mAtk";
    private const string AGI_KEY = "agi";
    private const string NB_KEY = "nB";

    //這個字典以 int 為 Key (heroId)，以 HeroJsonData 為 Value
    private Dictionary<int, HeroStatsJsonData> statsDataContainer = new Dictionary<int, HeroStatsJsonData>();

    protected override void FillJsonContainer(List<Dictionary<string, object>> rowDataList)
    {
        FillHeroStatsJsonDataInContainer(rowDataList);
    }

    //為 HeroJsonDataContainer 對應 id 的 HeroJsonData 配值
    //在此階段各等級數值還維持 google sheet 上的變量寫法
    void FillHeroStatsJsonDataInContainer(List<Dictionary<string, object>> rowDataList) 
    {
        statsDataContainer.Clear(); //清空再進行配值

        for (int i = 0; i < rowDataList.Count; i++)
        {
            var rowData = rowDataList[i];

            //id：作為索引，用於尋找 HeroDataContainer 對應的 HeroJsonData
            int row_id = CommonConverter.ToInt(rowData[ID_KEY]);

            //若 HeroDataContainer 不存在這個 ID，直接初始化一個
            if (!statsDataContainer.ContainsKey(row_id))
            {
                statsDataContainer[row_id] = new HeroStatsJsonData
                {
                    pos = "",
                    maxHp_increment = new List<int>(),
                    maxSp_increment = new List<int>(),
                    pAtk_increment = new List<int>(),
                    mAtk_increment = new List<int>(),
                    agi_increment = new List<int>(), 
                };  
            }

            var statsData = statsDataContainer[row_id];

            //目前迭代到的列資料的等級
            int row_level = CommonConverter.ToInt(rowData[LEVEL_KEY]);  
            
            if (row_level == 1)
            {
                //排位在等級1時配值即可
                string row_pos = rowData[POS_KEY].ToString().Trim();                //排位英文
                statsData.pos = row_pos;
            }

            //以下欄位：第 1 等為初始值、第 2 ~ 6 等數值為等級變量
            int row_maxHP = CommonConverter.ToInt(rowData[MAX_HP_KEY]);
            int row_maxSP = CommonConverter.ToInt(rowData[MAX_SP_KEY]);
            int row_pAtk = CommonConverter.ToInt(rowData[P_ATK_KEY]);
            int row_mAtk = CommonConverter.ToInt(rowData[M_ATK_KEY]);
            int row_agi = CommonConverter.ToInt(rowData[AGI_KEY]);

            statsData.maxHp_increment.Add(row_maxHP);
            statsData.maxSp_increment.Add(row_maxSP);
            statsData.pAtk_increment.Add(row_pAtk);
            statsData.mAtk_increment.Add(row_mAtk);
            statsData.agi_increment.Add(row_agi);
        }
    }


    [ContextMenu("同步所有英雄-角色基本數值[程式]")]
    public void SyncAllHeroData()
    {
        HeroManager manager;
        manager = HeroManager.Instance ? HeroManager.Instance : FindAnyObjectByType<HeroManager>();
        if (manager == null)
        {
            Debug.LogWarning("Hero Data Manager 為空");
            return;
        }
        StartCoroutine(DelayedAndSyncAllHeroes(manager.AllHeroes.ToList()));
    }

    private IEnumerator DelayedAndSyncAllHeroes(List<HeroData> heroes)
    {
        yield return StartCoroutine(GetRequest());
        foreach (var hero in heroes)
        {
            GetSingleJsonDataAndSync(hero, true);
        }
    }

    //取得單一個 HeroJsonData，並同步到 HeroData 中
    private void GetSingleJsonDataAndSync(HeroData heroData, bool markDirty)
    {
        if(heroData == null) return;
        
        if (statsDataContainer.TryGetValue(heroData.heroId, out var foundJsonData))
        {
            //同步排位
            //heroData.PosType = GetHeroPosType(foundJsonData.pos);

            //將變量轉換成各等級數值並同步
            try
            {
                heroData.baseMaxHPPerLevel = ConvertIncrementsToPerLevelValues(foundJsonData.maxHp_increment).ToArray();
                heroData.baseMaxSPPerLevel = ConvertIncrementsToPerLevelValues(foundJsonData.maxSp_increment).ToArray();

                heroData.basePAtkPerLevel = ConvertIncrementsToPerLevelValues(foundJsonData.pAtk_increment).ToArray();
                heroData.baseMAtkPerLevel = ConvertIncrementsToPerLevelValues(foundJsonData.mAtk_increment).ToArray();
                heroData.baseAgiPerLevel = ConvertIncrementsToPerLevelValues(foundJsonData.agi_increment).ToArray();
      
            }
            catch (Exception ex)
            {
                Debug.LogError($"英雄id：{heroData.heroId}，{ex.Message}");
            }

            if(markDirty) heroData.MarkAsDirty();

            Debug.Log($"角色基本數值同步成功!，英雄：{heroData.heroId} - {heroData.archetypeName}");
        }
        else
        {
            Debug.LogWarning($"找不到英雄ID為 {heroData.heroId} 的 JSON data!");
        }
    }

    private List<int> ConvertIncrementsToPerLevelValues(List<int> incrementList)
    {
        if(incrementList == null || incrementList.Count < 0)
        {
            Debug.LogWarning("變量清單有問題");
            return new List<int>();
        }

        var levelValues = new List<int>(incrementList.Count);

        //添加第一個元素：等級為1時的初始值
        levelValues.Add(incrementList[0]);

        //其餘元素：前一個元素加上變量值
        for(int i = 1; i < incrementList.Count; i++)
        {
            int addedValue = levelValues[i - 1] + incrementList[i];
            levelValues.Add(addedValue);
        }

        return levelValues;
    }


    //將 HeroJsonData 的 pos 從字串轉為 HeroPosType
    private HeroPosType GetHeroPosType(string pos)
    {
        switch (pos.Trim())
        {
            case "F":
                return HeroPosType.Front;

            case "A":
                return HeroPosType.Any;

            case "B":
                return HeroPosType.Back;
        }
        return HeroPosType.Any;
    }
}