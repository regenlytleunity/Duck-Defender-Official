using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UIUpdateVerification
{
    const string SnapshotKey = "DuckDefender.UIVerificationPrefs";
    [Serializable] class PreferenceSnapshot { public Entry[] entries; }
    [Serializable] class Entry { public string key, text; public bool exists, floating, isString; public int integer; public float number; }
    static int _checks;
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("UI verification: " + message); _checks++; }
    static UIUpdateVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            string json = SessionState.GetString(SnapshotKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                foreach (var e in JsonUtility.FromJson<PreferenceSnapshot>(json).entries)
                {
                    if (!e.exists) PlayerPrefs.DeleteKey(e.key);
                    else if (e.isString) PlayerPrefs.SetString(e.key,e.text);
                    else if (e.floating) PlayerPrefs.SetFloat(e.key,e.number);
                    else PlayerPrefs.SetInt(e.key,e.integer);
                }
                PlayerPrefs.Save();
                AudioListener.volume = PlayerPrefs.GetFloat("DuckDefender_MasterVolume",1);
                SessionState.EraseString(SnapshotKey);
            }
            SessionState.EraseString(UIUpdatePlayProbe.SessionKey);
            SaveSystem.VerificationSavePath = null;
            GameDifficulty.Load();
        };
    }
    [MenuItem("Duck Defender/UI/Start Isolated Play Verification")]
    public static void StartPlay()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene changes first.");
        string[] floats={"DuckDefender_MasterVolume","DuckDefender_SFXVolume","DuckDefender_MusicVolume",WorldCamera.ZoomPreference};
        string[] ints={GameDifficulty.PreferenceKey,ParticleVisibility.PreferenceKey,"Key_MoveLeft","Key_MoveRight","Key_Jump","Key_Crouch","Key_Dash","Key_Shoot","Key_Pause","Key_MoveLeftAlt","Key_MoveRightAlt","Key_JumpAlt","Key_CrouchAlt"};
        var strings=ControllerBindings.Actions.Select(a=>ControllerBindings.Prefix+a).ToArray();
        var entries=floats.Concat(ints).Concat(new[]{ControllerBindings.Prefix+"RightMoveStick"}).Concat(strings).Select(k=>new Entry{key=k,exists=PlayerPrefs.HasKey(k),floating=floats.Contains(k),isString=strings.Contains(k),text=PlayerPrefs.GetString(k),integer=PlayerPrefs.GetInt(k),number=PlayerPrefs.GetFloat(k)}).ToArray();
        SessionState.SetString(SnapshotKey,JsonUtility.ToJson(new PreferenceSnapshot{entries=entries}));
        SessionState.SetString(UIUpdatePlayProbe.SessionKey,Path.Combine(Path.GetTempPath(),"duck-ui-"+Guid.NewGuid().ToString("N")+".json"));
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying=true;
    }
    public static int CheckCurrentPointers()
    {
        int count=0;
        var menu=MainMenuUI.Instance;
        GameObject scope=menu.gameObject;
        if(menu.SettingsPanel.activeSelf)
        {
            var settings=menu.SettingsPanel.GetComponent<SettingsMenuUI>();
            if(settings.ResetConfirmationPanel.activeSelf)scope=settings.ResetConfirmationPanel;
            else if(settings.KeybindPanel.activeSelf)scope=settings.KeyCapturePanel.activeSelf?settings.KeyCapturePanel:settings.KeybindPanel;
        }
        if(menu.ConfirmPanel.activeSelf)scope=menu.ConfirmPanel;
        if(menu.OpeningOverlay.activeSelf)scope=menu.OpeningOverlay;
        foreach(var control in scope.GetComponentsInChildren<UnityEngine.UI.Selectable>())
        {
            if(!control.IsActive()||!control.IsInteractable())continue;
            CheckHit(control);count++;
        }
        return count;
    }
    static void CheckHit(UnityEngine.UI.Selectable target)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)target.transform;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {position=RectTransformUtility.WorldToScreenPoint(target.GetComponentInParent<Canvas>().renderMode==RenderMode.ScreenSpaceOverlay?null:target.GetComponentInParent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>()==target,"unblocked pointer target: "+target.name);
    }
    static void CheckIndexProgression(MainMenuUI menu,ShopManager shop)
    {
        var data=SaveSystem.LoadData();data.TotalCoins=10000;
        data.CardCollection=shop.AllCards.Where(c=>c!=null&&!c.IsBasic).Select(c=>new CardSaveData(c.ID){Duplicates=100}).ToList();
        foreach(CardPackType pack in new[]{CardPackType.Munitions,CardPackType.Mobility,CardPackType.Survival,CardPackType.Gadget})data.AddEssence(pack,2000);
        SaveSystem.SaveData(data);shop.LoadEconomy();
        var index=menu.IndexPanel.GetComponent<CardIndexUI>();
        int displayed=0;
        for(int pack=0;pack<4;pack++)
        {
            index.SelectPack(pack);
            do
            {
                Canvas.ForceUpdateCanvases();
                foreach(var display in index.ContentArea.GetComponentsInChildren<CardDisplay>())
                {
                    display.DescriptionText.ForceMeshUpdate();
                    Check(!display.DescriptionText.isTextOverflowing,"card description fits: "+display.NameText.text);
                    displayed++;
                }
                if(!index.NextButton.interactable)break;
                index.NextPage();
            }while(true);
        }
        Check(displayed==data.CardCollection.Count,"every collection card accessible once across pack pages");
        index.SelectPack(1);
        var first=shop.AllCards.Where(c=>c!=null&&!c.IsBasic&&c.PackCategory==CardPackType.Mobility).OrderBy(c=>c.Rarity).ThenBy(c=>c.CardName).First();
        var view=index.ContentArea.GetComponentsInChildren<CardDisplay>().First();
        int before=shop.CurrentCoins;view.UpgradeButton.onClick.Invoke();
        Check(shop.GetCardData(first.ID).Level==2&&shop.CurrentCoins==before-first.GetUpgradeCost(1),"visible card upgrade charges and advances");
        Check(index.CoinsText.text.StartsWith(shop.CurrentCoins.ToString("N0")),"index coins update after upgrade");
        var ascension=shop.AllCards.First(c=>c!=null&&!c.IsBasic&&c.Ascension!=CardAscension.None);
        shop.GetCardData(ascension.ID).Level=6;index.SelectPack((int)ascension.PackCategory);
        var ordered=shop.AllCards.Where(c=>c!=null&&!c.IsBasic&&c.PackCategory==ascension.PackCategory).OrderBy(c=>c.Rarity).ThenBy(c=>c.CardName).ToList();
        int page=ordered.IndexOf(ascension)/index.CardsPerPage;for(int i=0;i<page;i++)index.NextPage();
        view=index.ContentArea.GetComponentsInChildren<CardDisplay>().First(c=>c.NameText.text==ascension.CardName);
        Check(view.AscendButton.gameObject.activeSelf&&view.AscendButton.interactable,"ascension control available on paged card");
        before=shop.GetEssence(ascension.PackCategory);view.AscendButton.onClick.Invoke();
        Check(shop.GetCardData(ascension.ID).IsAscended&&shop.GetEssence(ascension.PackCategory)==before-ascension.AscensionCost,"ascension spends correct essence");
        Check(index.EssenceText.text.StartsWith(shop.GetEssence(ascension.PackCategory).ToString("N0")),"essence balance refreshes after ascension");
    }
    public static string CheckNativeSpriteSizing()
    {
        Check(EditorApplication.isPlaying && !string.IsNullOrEmpty(SaveSystem.VerificationSavePath),"isolated Play Mode required");
        _checks=0;
        var menu=MainMenuUI.Instance;
        var index=menu.IndexPanel.GetComponent<CardIndexUI>();
        string[] mainNames={"PlayButton","ShopButton","IndexButton","HostButton","JoinButton",
            "RankedButton","SettingsButton","LeaderboardButton","BackButton","KeybindsButton","ResetDataButton"};
        var sprite=menu.MenuPanel.transform.Find("PlayButton").GetComponent<UnityEngine.UI.Image>().sprite;
        var standard=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        float ppu=menu.GetComponent<Canvas>().referencePixelsPerUnit;
        foreach(var button in menu.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var image=button.GetComponent<UnityEngine.UI.Image>();
            if(image==null || button.GetComponentInParent<CardDisplay>()!=null || button.transform.parent==menu.PackContainer)continue;
            bool main=mainNames.Contains(button.name)||index.PackButtons.Contains(button);
            if(main)
            {
                Check(image.sprite==sprite && image.type==UnityEngine.UI.Image.Type.Simple && image.preserveAspect,"native button artwork: "+button.name);
                Check(Vector2.Distance(image.rectTransform.rect.size,sprite.rect.size*ppu/sprite.pixelsPerUnit*2)<.01f,"double-size button dimensions: "+button.name);
                Check(button.transform.localScale==Vector3.one && image.rectTransform.anchorMin==image.rectTransform.anchorMax,"unstretched button transform: "+button.name);
            }
            else Check(image.sprite==standard,"default secondary button texture: "+button.name);
        }
        var logo=menu.MenuPanel.transform.Find("Game Logo").GetComponent<UnityEngine.UI.Image>();
        Check(Vector2.Distance(logo.rectTransform.rect.size,logo.sprite.rect.size*ppu/logo.sprite.pixelsPerUnit)<.01f && logo.transform.localScale==Vector3.one && logo.preserveAspect,"native logo proportions");
        Check(index.CardsPerPage==3,"three-card page capacity");
        menu.OpenIndex();index.SelectPack((int)CardPackType.Mobility);Canvas.ForceUpdateCanvases();
        var cards=index.ContentArea.GetComponentsInChildren<CardDisplay>();
        Check(cards.Length==3,"three visible cards");
        foreach(var card in cards)
        {
            var rect=(RectTransform)card.transform;
            Check(Mathf.Approximately(rect.anchorMin.y,.5f),"single centered card row");
            Check(rect.localScale.x>.8f && Mathf.Approximately(rect.localScale.x,rect.localScale.y),"larger cards with preserved proportions");
        }
        return _checks+" double-size buttons and default index layout checks passed";
    }

    public static string CheckIndexLayouts()
    {
        Check(EditorApplication.isPlaying && !string.IsNullOrEmpty(SaveSystem.VerificationSavePath),"isolated Play Mode required");
        _checks=0;
        var menu=MainMenuUI.Instance;
        menu.OpenIndex();
        var index=menu.IndexPanel.GetComponent<CardIndexUI>();
        Check(index.LayoutButton!=null && index.LayoutText!=null,"layout button and label assigned");
        Check(index.LayoutButton.onClick.GetPersistentEventCount()==1,"one serialized layout callback");
        Check(index.LayoutText.color==Color.white,"layout text has white infill");
        int[] columns={3,4},rows={1,2};
        for(int layout=0;layout<3;layout++)
        {
            Check(layout == 2 ? index.PageCount == 1 : index.LayoutColumns==columns[layout] && index.LayoutRows==rows[layout],"requested layout cycle order");
            Check(index.LayoutText.text==(layout == 2 ? "LAYOUT: ALL" : "LAYOUT: "+columns[layout]+" x "+rows[layout]),"current layout label");
            CheckIndexProgression(menu,ShopManager.Instance);
            for(int pack=0;pack<4;pack++)
            {
                index.PackButtons[pack].onClick.Invoke();
                var expected=ShopManager.Instance.AllCards.Where(c=>c!=null&&!c.IsBasic&&(int)c.PackCategory==pack)
                    .OrderBy(c=>c.Rarity).ThenBy(c=>c.CardName).ToArray();
                Check(index.PageCount==Mathf.CeilToInt(expected.Length/(float)index.CardsPerPage),"page count for layout and pack");
                if (layout == 2) Check(index.PageCount == 1 && index.CardsPerPage >= expected.Length, "ALL fits the complete category");
                for(int page=0;page<index.PageCount;page++)
                {
                    Canvas.ForceUpdateCanvases();
                    var cards=index.ContentArea.GetComponentsInChildren<CardDisplay>();
                    Check(cards.Length==Mathf.Min(index.CardsPerPage,expected.Length-page*index.CardsPerPage),"full and partial page capacity");
                    Check(cards.Select(c=>c.NameText.text).SequenceEqual(expected.Skip(page*index.CardsPerPage).Take(index.CardsPerPage).Select(c=>ShopManager.Instance.GetCardData(c.ID).IsAscended?c.AscendedName:c.CardName)),"ordered cards without omissions");
                    Check(index.PreviousButton.interactable==(page>0) && index.NextButton.interactable==(page+1<index.PageCount),"pagination button boundaries");
                    var area=(RectTransform)index.ContentArea;
                    for(int i=0;i<cards.Length;i++)
                    {
                        var rect=(RectTransform)cards[i].transform;
                        var position=new Vector2((i%index.LayoutColumns+.5f)/index.LayoutColumns,1-(i/index.LayoutColumns+.5f)/index.LayoutRows);
                        Check(Vector2.Distance(rect.anchorMin,position)<.001f && rect.anchorMin==rect.anchorMax,"card row and column");
                        Check(Mathf.Approximately(rect.localScale.x,rect.localScale.y) && rect.rect.width*rect.localScale.x<area.rect.width/index.LayoutColumns && rect.rect.height*rect.localScale.y<area.rect.height/index.LayoutRows,"cards fit cells without distortion");
                    }
                    index.NextButton.onClick.Invoke();
                }
                int last=index.CurrentPage;index.NextPage();Check(index.CurrentPage==last,"last page cannot advance");
                while(index.PreviousButton.interactable)index.PreviousButton.onClick.Invoke();
                index.PreviousPage();Check(index.CurrentPage==0,"first page cannot retreat");
            }
            index.SelectPack((int)CardPackType.Mobility);
            while(index.NextButton.interactable)index.NextButton.onClick.Invoke();
            int firstCard=index.CurrentPage*index.CardsPerPage;
            string layoutLabel = index.LayoutText.text; menu.OpenMenu();menu.OpenIndex();
            Check(index.LayoutText.text==layoutLabel,"layout retained after closing index");
            // Pointer checks run after rendering; same-frame reopen has no graphic depth yet.
            index.LayoutButton.onClick.Invoke();
            int start=index.CurrentPage*index.CardsPerPage;
            Check(start<=firstCard && firstCard<start+index.CardsPerPage,"layout change retains previous first card");
            Check(index.CurrentPage<index.PageCount,"layout change clamps page");
        }
        Check(index.LayoutColumns==3 && index.LayoutRows==1,"layout cycle wraps to 3x1");
        menu.OpenMenu();
        return _checks+" index layout checks passed";
    }

    public static string CheckMobileControls(bool expectedVisible = false)
    {
        Check(EditorApplication.isPlaying && !string.IsNullOrEmpty(SaveSystem.VerificationSavePath),"isolated Play Mode required");
        _checks=0;
        var mobile=MobileInputController.Instance;
        Check(mobile!=null && mobile.isActiveAndEnabled,"mobile visibility controller survives duplicate input removal");
        Check(mobile.IsMobileEnabled==expectedVisible,"device input mode");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true))
            .Single(c=>c.name==mobile.CanvasName);
        Check(canvas.gameObject.activeSelf==expectedVisible,"touch canvas visibility");
        Check(mobile.MoveJoystick!=null && mobile.AimJoystick!=null,"both joystick references retained");
        Check(InputManager.Instance!=null && InputManager.Instance.isActiveAndEnabled,"desktop input owner retained");
        Check(UnityEngine.Object.FindObjectsByType<InputManager>(FindObjectsSortMode.None).Count(i=>i.isActiveAndEnabled)==1,"one active input manager");
        var mobileQuery=typeof(InputHelper).GetProperty("IsMobile",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Check((bool)mobileQuery.GetValue(null)==expectedVisible,"InputHelper uses the same device mode as the canvas");
        return _checks+" mobile visibility checks passed";
    }

    public static string CheckGameplay()
    {
        Check(EditorApplication.isPlaying && !string.IsNullOrEmpty(SaveSystem.VerificationSavePath),"isolated Play Mode required");
        _checks=0;
        var wave=WaveManager.Instance;var level=LevelManager.Instance;
        Check(wave!=null&&level!=null&&PlayerController.Instance!=null,"Play button loaded gameplay");
        Check(UnityEngine.Object.FindObjectsByType<InputManager>(FindObjectsSortMode.None).Count(i=>i.isActiveAndEnabled)==1,"input manager survives scene transition without duplicates");
        Check(GameDifficulty.Selected==RunDifficulty.Hard,"selected difficulty survives scene transition");
        var staging=new GameObject("UI verification enemies");staging.SetActive(false);
        var speedField=typeof(EnemyBase).GetField("CurrentSpeed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var healthField=typeof(PlayerHealth).GetField("_currentHealth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var invulnerable=typeof(PlayerHealth).GetField("_isInvulnerable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var player=PlayerController.Instance.GetComponent<PlayerHealth>();
        int oldHealth=player.CurrentHealth;float oldTime=Time.timeScale;Time.timeScale=1;
        try
        {
            for(int difficulty=0;difficulty<3;difficulty++)
            {
                GameDifficulty.Select(difficulty,false);
                float multiplier=GameDifficulty.HealthMultiplier;
                foreach(int number in new[]{1,20}) foreach(var prefab in wave.EnemyPrefabs)
                {
                    if(prefab==null)continue;
                    var enemy=UnityEngine.Object.Instantiate(prefab,staging.transform).GetComponent<EnemyBase>();enemy.HealthBarPrefab=null;
                    enemy.Initialize(number);
                    float baseHealth=enemy is TankEnemy ? 4*wave.BasicGroundHealth(number) : Mathf.Floor(enemy.BaseHealth+WaveManager.HealthIncreaseAtWave(number));
                    Check(Mathf.Approximately(enemy.MaxHealth,baseHealth*multiplier),"enemy health: "+enemy.EnemyKind+" difficulty "+difficulty+" wave "+number);
                    float baseSpeed=enemy is TankEnemy ? wave.BasicGroundSpeed()*((TankEnemy)enemy).GroundSpeedMultiplier : enemy.BaseSpeed;
                    Check(Mathf.Approximately((float)speedField.GetValue(enemy),baseSpeed*GameDifficulty.SpeedMultiplier),"enemy speed: "+enemy.EnemyKind);
                }
                // Use whole multiples to leave no test remainder between difficulty cases.
                int before=level.TotalCoins;for(int i=0;i<4;i++)level.AddCoins(1);
                Check(level.TotalCoins-before==Mathf.RoundToInt(4*GameDifficulty.CoinMultiplier),"single coin fractional rewards "+difficulty);
                before=level.TotalCoins;level.AddCoins(4);
                Check(level.TotalCoins-before==Mathf.RoundToInt(4*GameDifficulty.CoinMultiplier),"merged coin rewards "+difficulty);
                healthField.SetValue(player,5);invulnerable.SetValue(player,false);
                Check(player.TryTakeDamage(1)&&player.CurrentHealth==5-(int)GameDifficulty.DamageMultiplier,"enemy damage "+difficulty);
            }
            level.FlushCoinSave();Check(SaveSystem.LoadData().TotalCoins==level.TotalCoins,"difficulty rewards persist");
            Check(GameUI.Instance!=null&&GameUI.Instance.CoinText.text.Contains(level.TotalCoins.ToString("N0")),"coin HUD matches credited reward");
        }
        finally
        {
            UnityEngine.Object.Destroy(staging);GameDifficulty.Select(2,false);healthField.SetValue(player,oldHealth);Time.timeScale=oldTime;
        }
        return _checks+" gameplay difficulty checks passed";
    }
    public static string CheckMenuFlows()
    {
        Check(EditorApplication.isPlaying && !string.IsNullOrEmpty(SaveSystem.VerificationSavePath),"isolated Play Mode required");
        _checks=0;
        var menu=MainMenuUI.Instance;var shop=ShopManager.Instance;
        Check(menu!=null && shop!=null,"menu owners alive");
        var settings=menu.SettingsPanel.GetComponent<SettingsMenuUI>();
        Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"one EventSystem");
        Check(menu.GetComponent<UnityEngine.UI.GraphicRaycaster>()!=null,"canvas raycaster");
        for(int i=0;i<3;i++)
        {
            menu.DifficultyButtons[i].onClick.Invoke();
            Check((int)GameDifficulty.Selected==i,"difficulty button "+i);
            for(int j=0;j<3;j++) Check(menu.DifficultyButtons[j].GetComponent<UnityEngine.UI.Outline>().enabled==(i==j),"exclusive difficulty border");
        }
        Check(GameDifficulty.HealthMultiplier==2 && GameDifficulty.DamageMultiplier==2 && GameDifficulty.SpeedMultiplier==1.1f,"Hard modifiers");
        float hardGrowth=WaveManager.HealthIncreaseAtWave(20);menu.SelectDifficulty(0);
        Check(Mathf.Approximately(hardGrowth,WaveManager.HealthIncreaseAtWave(20)*2),"Hard doubles health growth");
        Check(menu.MenuPanel.transform.Find("HostButton").GetComponent<UnityEngine.UI.Button>().interactable,"host enabled");
        foreach(string name in new[]{"JoinButton","RankedButton","LeaderboardButton"})
            Check(!menu.MenuPanel.transform.Find(name).GetComponent<UnityEngine.UI.Button>().interactable,"disabled "+name);
        menu.OpenShop();
        Check(menu.PackContainer.childCount==4,"four shop packs");
        var grid=menu.PackContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>();Check(grid.constraintCount==2,"two shop columns");
        var pack=shop.AvailablePacks[0];var buy=menu.PackContainer.GetChild(0).GetComponent<UnityEngine.UI.Button>();
        buy.onClick.Invoke();Check(menu.ConfirmPanel.activeSelf && !menu.YesButton.interactable,"unaffordable pack blocked");
        menu.OpenShop();
        var data=SaveSystem.LoadData();data.TotalCoins=2000;SaveSystem.SaveData(data);shop.LoadEconomy();menu.UpdateCoinDisplay(shop.CurrentCoins);
        buy.onClick.Invoke();int before=shop.CurrentCoins;menu.YesButton.onClick.Invoke();Check(shop.CurrentCoins==before-pack.Cost,"one pack charged once");
        menu.YesButton.onClick.Invoke();Check(shop.CurrentCoins==before-pack.Cost,"duplicate purchase blocked");menu.OpenShop();
        buy.onClick.Invoke();before=shop.CurrentCoins;menu.BuyThreeButton.onClick.Invoke();Check(shop.CurrentCoins==before-pack.Cost*3,"three packs charged");menu.OpenShop();
        menu.OpenIndex();var index=menu.IndexPanel.GetComponent<CardIndexUI>();
        for(int i=0;i<4;i++)
        {
            index.PackButtons[i].onClick.Invoke();Check((int)index.SelectedPack==i && index.CurrentPage==0,"pack selection resets page");
            int count=shop.AllCards.Count(c=>c!=null&&!c.IsBasic&&(int)c.PackCategory==i);
            Check(index.PageCount==Mathf.CeilToInt(count/(float)index.CardsPerPage),"page count");
            Check(index.ContentArea.Cast<Transform>().Count(t=>t.gameObject.activeSelf)==Mathf.Min(index.CardsPerPage,count),"layout capacity");
            while(index.NextButton.interactable) index.NextButton.onClick.Invoke();
            Check(!index.NextButton.interactable && index.CurrentPage==index.PageCount-1,"last page boundary");
            index.NextPage();Check(index.CurrentPage==index.PageCount-1,"cannot advance beyond last page");
            while(index.PreviousButton.interactable) index.PreviousButton.onClick.Invoke();
            Check(index.CurrentPage==0&&!index.PreviousButton.interactable,"first page boundary");
            Check(index.EssenceText.text.Contains(((CardPackType)i).ToString()),"pack-specific balance");
        }
        CheckIndexProgression(menu, shop);
        menu.OpenSettings();
        settings.MasterSlider.value=.37f;Check(Mathf.Approximately(AudioListener.volume,.37f),"master volume applies");
        settings.SfxSlider.value=.42f;settings.MusicSlider.value=.28f;
        Check(Mathf.Approximately(AudioManager.Instance.GetSFXVolume(),.42f)&&Mathf.Approximately(AudioManager.Instance.GetMusicVolume(),.28f),"audio channels independent");
        settings.ShowTipsToggle.isOn=false;Check(!SaveSystem.LoadData().ShowTips,"tips saved");
        settings.ParticlesToggle.isOn=false;Check(!ParticleVisibility.Enabled,"particles off");
        var fx=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/Explosion Effect.prefab"));fx.SetActive(true);
        Check(fx.GetComponentsInChildren<ParticleSystemRenderer>(true).All(p=>p.forceRenderingOff),"new particles hidden");
        settings.ParticlesToggle.isOn=true;Check(fx.GetComponentsInChildren<ParticleSystemRenderer>(true).All(p=>!p.forceRenderingOff),"existing particles restored");UnityEngine.Object.Destroy(fx);
        settings.OpenKeybinds();Check(settings.KeybindPanel.activeSelf,"keybind menu opens");
        Check(!settings.MasterSlider.IsInteractable(),"keybind modal blocks background keyboard navigation");
        settings.BeginBinding(0);
        Check(!settings.KeybindPanel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.IsInteractable()),"key capture blocks underlying buttons");
        settings.CloseKeybinds();settings.OpenKeybinds();
        settings.RestoreKeybinds();Check(settings.TryAssignBinding(0,KeyCode.J),"keybind changed");
        InputManager.Instance.LoadKeybinds();Check(InputManager.Instance.GetKeybind("moveleft")==KeyCode.J,"keybind persisted");
        Check(!settings.TryAssignBinding(1,KeyCode.J),"conflicting key rejected");
        Check(settings.TryAssignBinding(6,KeyCode.K),"alternate key saved");
        settings.RestoreKeybinds();settings.CloseKeybinds();
        before=shop.CurrentCoins;settings.ShowResetConfirmation();Check(shop.CurrentCoins==before,"reset prompt does not mutate data");
        Check(!settings.MasterSlider.IsInteractable(),"reset modal blocks background keyboard navigation");
        Check(settings.ResetConfirmationPanel.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>b.IsInteractable()),"reset choices remain interactive");
        settings.CancelReset();settings.ConfirmReset();Check(shop.CurrentCoins==before,"cancel blocks reset");
        settings.ShowResetConfirmation();
        settings.ConfirmReset();
        Check(shop.CurrentCoins==0&&SaveSystem.LoadData().TotalCoins==0,"confirmed reset saved");
        Check(!settings.ResetConfirmationPanel.activeSelf,"reset modal closes");
        Check(settings.MasterSlider.IsInteractable(),"settings interaction restored after modal closes");
        Check(SaveSystem.LoadData().CardCollection.Count==8,"reset restores eight starters");
        menu.OpenMenu();return _checks+" Play Mode UI checks passed";
    }
}
