using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[System.Serializable]
public class BaseJsonData
{

}

public abstract class BaseJsonLoader : MonoBehaviour
{
    //一個試算表檔案只能有一個API，但可以用工作表名稱找特定工作表
    protected abstract string SheetName { get; }

    //API：Google app scripts發布後的網址
    protected abstract string ApiUrl { get; }

    protected IEnumerator GetRequest()
    {
        string uri = $"{ApiUrl}?sheet={SheetName}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;

                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;

                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
                    string _jsonResponse = webRequest.downloadHandler.text;
                    try
                    {
                        ParseJson(_jsonResponse);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("JSON Parsing Error: " + e.Message);
                    }
                    break;
            }
        }
    }

    protected void ParseJson(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse))
        {
            Debug.LogError("JSON Response is empty!");
            return;
        }

        try
        {
            List<Dictionary<string, object>> rowDictList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonResponse);

            /*
                JsonConvert.DeserializeObject會將jsonResponse解析 (反序列化 https://ithelp.ithome.com.tw/articles/10230992)

                1   原jsonResponse = 
                    [
                        {"ID":1,"Profession":"Knight","Level":1,"POS":"F","PAtk":80,"MAtk":60},
                        {"ID":1,"Profession":"Knight","Level":2,"POS":"F","PAtk":3,"MAtk":1},
                        ...
                        {"ID":1,"Profession":"Shaman","Level":1,"POS":"F","PAtk":72,"MAtk":80},
                        ... 
                    ]

                2   解析後的每一列都是一筆字典型態rowDict 
                    這個字典以【試算表的標題列(在googleAppsScript中定義)】為Key，例如"ID", "Profession", "Level", "POS","PAtk"...，
                    以object類型為value，因為儲存格有不同類型的數據int, string...
                    rowDict = new Dictionary<string,object>() { { "Profession", "Knight" }, { "Level", 1 }, { "POS", "F" }, { "PAtk", 80 }, { "MAtk", 60 } }

                3   由很多筆rowDict組成為rowDictList
                    因此rowDictList = List<Dictionary<string, object>> 
                    {
                        rowDictList[0] = { { "Profession", "Knight" }, { "Level", 1 }, { "POS", "F" }, { "PAtk", 80 }, { "MAtk", 60 } },
                        rowDictList[1] = { { "Profession", "Knight" }, { "Level", 2 }, { "POS", "F" }, { "PAtk", 3 }, { "MAtk", 1 } },
                        ...
                        rowDictList[n] = { { "Profession", "Shaman" }, { "Level", 1 }, { "POS", "F" }, { "PAtk", 72 }, { "MAtk", 80 } }
                        ...
                    };
            */

            if (rowDictList == null || rowDictList.Count == 0)
            {
                Debug.LogError("Parsed JSON list is null or empty.");
                return;
            }

            FillJsonContainer(rowDictList);
        }
        catch (Exception e)
        {
            Debug.LogError("JSON Parsing Error: " + e.Message + "\nStack Trace: " + e.StackTrace);
        }
    }

    protected abstract void FillJsonContainer(List<Dictionary<string, object>> rowDataList);
}
