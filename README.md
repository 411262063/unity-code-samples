# unity-code-samples

Code samples from my graduation project "Restart: Divergent Path" (Unity / C#)



以下程式碼節錄自大學畢業專題《異途重啟》，一款結合爬塔型 Roguelike 與角色抽卡的 PC 遊戲（6 人團隊，本人擔任程式）。

作品已於 2026 年新一代設計展展出，並發布於 **itch.io** ([https://kuan3899.itch.io/restartdivergentpath](https://kuan3899.itch.io/restartdivergentpath))。



此處收錄的皆為本人撰寫的部分。程式碼依賴專案中的其他類別（如 HeroManager、HeroData），無法單獨執行，主要用於呈現設計思路。



**## 1. InputHandler — 輸入事件廣播**

【用途】

－搭配 Unity Input System，統一處理遊戲的基礎輸入，常用於非 UI Navigation 的物件導航使用。

【設計重點】

－為遊戲全局管理物件 GameHandler (DDOL) 的子物件，提供跨場景一致的輸入來源

－將輸入轉為事件廣播，區分單次觸發（+1 / -1）與持續按住兩種型態，訂閱者只需監聽事件，不必直接依賴 Input System

－支援鍵盤與手把雙裝置



**## 2. GachaStateManager — 抽卡流程狀態機**

【用途】

－管理抽卡場景的流程，依玩家操作推進至不同階段。

【設計重點】

－以玩家操作劃分各階段，每個階段各自控制影片播放、Animator 狀態與輸入限制

－避免在單一 Update 中堆疊大量條件判斷，讓流程新增或調整時只需修改對應狀態



**## 3. SheetDataSync — Google Sheet 數值同步（已停用）**

【用途】

－開發初期，讓企劃在 Google Sheet 調整的角色數值，能一鍵同步到各英雄的 ScriptableObject。

【架構】

－SheetToJson：Google Apps Script，將指定工作表輸出為 JSON

－BaseJsonLoader：抽象基底類別，負責 HTTP 請求與 JSON 解析的共用流程（Template Method）

－HeroStatsJsonLoader：繼承基底類別，負責將資料轉換並寫入英雄資料；透過 ContextMenu 在編輯器中執行

【為什麼停用】

此做法要求每筆資料位於獨立儲存格，但企劃的表格習慣大量使用合併儲存格，若要沿用，企劃需額外維護一份專供匯出的表格。評估製作時程後，這份重工成本不划算，最後改為直接在 Unity 中調整數值。

註：API 網址已移除。

