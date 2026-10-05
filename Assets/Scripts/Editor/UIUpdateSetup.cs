using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

// Repeatable authoring operation: edits the existing menu via Unity APIs, never YAML.
public static class UIUpdateSetup
{
    static TMP_FontAsset _font;
    static Sprite _buttonSprite;
    static readonly Color Ink = new Color(.08f, .13f, .20f);
    static readonly Color Paper = new Color(.82f, .90f, .98f);

    [MenuItem("Duck Defender/UI/Apply October Layout")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/MainMenu.unity") throw new InvalidOperationException("Open MainMenu first.");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        if (menu == null) throw new InvalidOperationException("MainMenuUI is missing.");
        Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "October UI layout");
        var safe = menu.transform.Find("SafeArea");
        _font = menu.TotalCoinsText.font;
        _buttonSprite = safe.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "PlayButton").GetComponent<UnityEngine.UI.Image>().sprite;
        BuildMenu(menu, safe);
        BuildShop(menu);
        BuildIndex(menu.IndexPanel.GetComponent<CardIndexUI>());
        ConfigureIndexCard(menu.IndexPanel.GetComponent<CardIndexUI>().CardDisplayPrefab);
        BuildSettings(menu.SettingsPanel.GetComponent<SettingsMenuUI>());
        if (UnityEngine.Object.FindFirstObjectByType<InputManager>() == null) new GameObject("InputManager").AddComponent<InputManager>();
        menu.MenuPanel.SetActive(true);
        menu.ShopPanel.SetActive(false); menu.IndexPanel.SetActive(false); menu.SettingsPanel.SetActive(false);
        menu.ConfirmPanel.SetActive(false); menu.OpeningOverlay.SetActive(false);
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ConfigurePackPrefab(menu.PackButtonPrefab);
        ConfigureParticles();
        AssetDatabase.SaveAssets();
        Debug.Log("[UI Update] Menu, shop, index, settings and particle preferences wired and saved.");
    }

    static RectTransform Node(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    static void Area(Transform target, float left, float bottom, float right, float top)
    {
        var rect = (RectTransform)target;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    static void LabelStyle(TextMeshProUGUI label, string text, float size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        label.text = text; label.font = _font; label.color = Ink;
        label.fontSize = size; label.enableAutoSizing = true; label.fontSizeMax = size; label.fontSizeMin = Mathf.Min(20, size);
        label.alignment = alignment; label.raycastTarget = false;
        label.margin = new Vector4(8, 3, 8, 3);
    }
    static TextMeshProUGUI Label(Transform parent, string name, string text, float size, float l, float b, float r, float t)
    {
        var rect = Node(parent, name); Area(rect,l,b,r,t);
        var label = rect.GetComponent<TextMeshProUGUI>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
        LabelStyle(label,text,size); return label;
    }
    static UnityEngine.UI.Image Panel(Transform parent, string name, Color color)
    {
        var rect = Node(parent,name); Area(rect,0,0,1,1);
        var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = null; image.color = color; image.raycastTarget = true; return image;
    }
    static UnityEngine.UI.Button Button(Transform parent, string name, string text, float l, float b, float r, float t, UnityAction click = null)
    {
        var rect = Node(parent,name); Area(rect,l,b,r,t);
        var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = _buttonSprite; image.color = Color.white; image.type = UnityEngine.UI.Image.Type.Sliced; image.raycastTarget = true;
        var button = rect.GetComponent<UnityEngine.UI.Button>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.colors = UnityEngine.UI.ColorBlock.defaultColorBlock;
        var colors = button.colors; colors.disabledColor = new Color(.53f,.57f,.61f,.8f); colors.fadeDuration = 0; button.colors = colors;
        button.interactable = true;
        var label = rect.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) label = Label(rect,"Label",text,32,0,0,1,1);
        else { Area(label.transform, .04f,.07f,.96f,.93f); LabelStyle(label,text,32); }
        label.textWrappingMode = TextWrappingModes.NoWrap; label.fontSizeMin = 14;
        if (click != null) { button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick,click); }
        if (rect.GetComponent<ButtonClickSound>() == null) rect.gameObject.AddComponent<ButtonClickSound>();
        return button;
    }
    static void IntAction(UnityEngine.UI.Button button, UnityAction<int> action, int value)
    {
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        UnityEventTools.AddIntPersistentListener(button.onClick, action, value);
        var border = button.GetComponent<UnityEngine.UI.Outline>() ?? button.gameObject.AddComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(1,.67f,.1f); border.effectDistance = new Vector2(4,-4); border.enabled = false;
    }
    static void BuildMenu(MainMenuUI menu, Transform safe)
    {
        var group = Node(safe,"MenuContent"); Area(group,0,0,1,1); group.SetAsFirstSibling();
        foreach (string name in new[] {"Background","Game Logo","Game Version","Dev Name","PlayButton","ShopButton","IndexButton","SettingsButton"})
        {
            var child = safe.Find(name);
            if (child != null) child.SetParent(group,false);
        }
        menu.MenuPanel = group.gameObject;
        Area(group.Find("Background"),0,0,1,1);
        group.Find("Background").localScale = new Vector3(-1,1,1); // Place the existing duck on the right, as sketched.
        Area(group.Find("Game Logo"),.38f,.65f,.94f,.94f);
        Area(group.Find("Game Version"),.73f,.60f,.93f,.66f);
        Area(group.Find("Dev Name"),.36f,.01f,.70f,.055f);
        Button(group,"PlayButton","PLAY",.06f,.76f,.30f,.85f);
        Button(group,"ShopButton","SHOP",.06f,.57f,.30f,.66f,menu.OpenShop);
        Button(group,"IndexButton","INDEX",.06f,.45f,.30f,.54f,menu.OpenIndex);
        string[] future = {"Host","Join","Ranked"};
        for (int i=0;i<3;i++) Button(group,future[i]+"Button",future[i].ToUpper(),.06f,.33f-i*.11f,.30f,.41f-i*.11f).interactable=false;
        Button(group,"LeaderboardButton","LEADERBOARD",.33f,.11f,.53f,.19f).interactable=false;
        Label(group,"ComingSoon","Online modes coming soon",24,.06f,.055f,.53f,.10f);
        Button(group,"SettingsButton","SETTINGS",.79f,.05f,.96f,.13f,menu.OpenSettings);
        menu.DifficultyButtons = new UnityEngine.UI.Button[3];
        for (int i=0;i<3;i++)
        {
            menu.DifficultyButtons[i]=Button(group,((RunDifficulty)i)+"Button",((RunDifficulty)i).ToString().ToUpper(),.06f+i*.082f,.69f,.136f+i*.082f,.745f);
            IntAction(menu.DifficultyButtons[i],menu.SelectDifficulty,i);
        }
        menu.DifficultyDescription=Label(group,"DifficultyDescription","1x coins | Standard experience",26,.33f,.31f,.61f,.56f);
        menu.DifficultyButtons[0].GetComponent<UnityEngine.UI.Outline>().enabled=true;
    }
    static void BuildShop(MainMenuUI menu)
    {
        var root = menu.ShopPanel.transform;
        var balances=Panel(root,"BalancesBackground",new Color(.89f,.94f,1f,.97f));Area(balances.transform,.015f,.10f,.255f,.83f);balances.raycastTarget=false;balances.transform.SetAsFirstSibling();
        Button(root,"BackButton","BACK",.035f,.87f,.20f,.95f,menu.BackToMainMenu);
        root.Find("The Shop Text").gameObject.SetActive(true);
        Area(root.Find("The Shop Text"),.31f,.88f,.95f,.98f);
        LabelStyle(root.Find("The Shop Text").GetComponent<TextMeshProUGUI>(),"CARD SHOP",48);
        Area(menu.TotalCoinsText.transform,.03f,.69f,.24f,.81f); LabelStyle(menu.TotalCoinsText,"0 Coins",34);
        if (menu.EssenceBalancesText != null) menu.EssenceBalancesText.gameObject.SetActive(false);
        menu.PackEssenceTexts=new TextMeshProUGUI[4];
        int[] order={0,1,3,2};
        for (int i=0;i<4;i++) menu.PackEssenceTexts[order[i]]=Label(root,"Essence_"+order[i],((CardPackType)order[i])+" Essence\n0",30,.025f,.53f-i*.14f,.25f,.65f-i*.14f);
        var packs=menu.PackContainer;
        Area(packs,.29f,.05f,.98f,.87f);
        var row=packs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(); if(row!=null) UnityEngine.Object.DestroyImmediate(row);
        var fitter=packs.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if(fitter!=null) UnityEngine.Object.DestroyImmediate(fitter);
        var grid=packs.GetComponent<UnityEngine.UI.GridLayoutGroup>()??packs.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize=new Vector2(430,400); grid.spacing=new Vector2(130,40); grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=2; grid.childAlignment=TextAnchor.MiddleCenter;
        // Use the sketch's order without changing definitions or prices.
        var shop=UnityEngine.Object.FindFirstObjectByType<ShopManager>();
        int[] packOrder={1,0,2,3};
        shop.AvailablePacks=shop.AvailablePacks.OrderBy(p=>Array.IndexOf(packOrder,(int)p.PackType)).ToList();
        EditorUtility.SetDirty(shop);
    }
    static void ConfigurePackPrefab(GameObject prefab)
    {
        string path=AssetDatabase.GetAssetPath(prefab);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            ((RectTransform)root.transform).sizeDelta=new Vector2(430,400);
            root.transform.localScale=Vector3.one;
            var image=root.GetComponent<UnityEngine.UI.Image>(); if(image!=null){image.color=new Color(1,1,1,.92f);image.raycastTarget=true;}
            foreach(var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if(text.name=="Price") {Area(text.transform,.1f,.01f,.9f,.13f);LabelStyle(text,"100 G",30);}
                else if(text.name=="Name") {Area(text.transform,.03f,.86f,.97f,.98f);LabelStyle(text,"Pack",32);}
                else text.gameObject.SetActive(false);
            }
            var icon=root.transform.Find("PackIcon");
            if(icon!=null){Area(icon,.20f,.15f,.80f,.83f);icon.GetComponent<UnityEngine.UI.Image>().preserveAspect=true;icon.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;}
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    static void BuildIndex(CardIndexUI index)
    {
        var root=index.transform;
        var bg=root.GetComponent<UnityEngine.UI.Image>();bg.sprite=null;bg.color=Paper;
        var scroll=root.Find("Scroll View"); if(scroll!=null) scroll.gameObject.SetActive(false);
        // Keep the old scroll hierarchy serialized for compatibility; pagination owns this content.
        index.ContentArea=Node(root,"PagedCards"); Area(index.ContentArea,.27f,.10f,.98f,.88f);
        Button(root,"BackButton","BACK",.035f,.87f,.20f,.95f,UnityEngine.Object.FindFirstObjectByType<MainMenuUI>().BackToMainMenu);
        index.CoinsText=Label(root,"IndexCoins","0 Coins",34,.28f,.90f,.51f,.98f);
        index.EssenceText=Label(root,"IndexEssence","0 Mobility Essence",34,.54f,.90f,.98f,.98f);
        index.PackButtons=new UnityEngine.UI.Button[4];int[] order={1,0,2,3};
        for(int i=0;i<4;i++)
        {
            int pack=order[i];var b=Button(root,((CardPackType)pack)+"Tab",((CardPackType)pack).ToString().ToUpper(),.025f,.66f-i*.14f,.235f,.75f-i*.14f);
            index.PackButtons[pack]=b;IntAction(b,index.SelectPack,pack);
        }
        index.PreviousButton=Button(root,"PreviousPage","<",.47f,.025f,.53f,.085f,index.PreviousPage);
        index.NextButton=Button(root,"NextPage",">",.72f,.025f,.78f,.085f,index.NextPage);
        index.PageText=Label(root,"PageNumber","1 / 1",30,.55f,.025f,.70f,.085f);
        EditorUtility.SetDirty(index);
    }
    static void SliderRow(Transform root, string name, float bottom, out UnityEngine.UI.Slider slider, out TextMeshProUGUI percent)
    {
        var row=root.Find(name); Area(row,.16f,bottom,.51f,bottom+.13f);
        slider=row.GetComponentInChildren<UnityEngine.UI.Slider>(true);
        Area(slider.transform,.03f,.12f,.83f,.49f); slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
        var labels=row.GetComponentsInChildren<TextMeshProUGUI>(true);
        var title=labels.First(t=>t.name.Contains("label")); Area(title.transform,0,.52f,1,1);
        LabelStyle(title,name=="Master Slider"?"Overall volume":name=="SFX Slider"?"Sound effects":"Background music",34);
        percent=labels.First(t=>t.name.Contains("precentage")); Area(percent.transform,.84f,.07f,1,.48f);LabelStyle(percent,"100%",24);
    }
    static void BuildSettings(SettingsMenuUI settings)
    {
        var root=settings.transform;
        if(root.GetComponent<CanvasGroup>()==null)root.gameObject.AddComponent<CanvasGroup>();
        var menu=UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        var volumePanel=Panel(root,"VolumeBackground",new Color(.89f,.94f,1f,.97f));Area(volumePanel.transform,.13f,.25f,.54f,.82f);volumePanel.raycastTarget=false;volumePanel.transform.SetAsFirstSibling();
        Button(root,"BackButton","BACK",.035f,.87f,.20f,.95f,menu.BackToMainMenu);
        Label(root,"SettingsTitle","SETTINGS",48,.3f,.87f,.7f,.97f);
        if(root.Find("Master Slider")==null){var copy=UnityEngine.Object.Instantiate(root.Find("SFX Slider").gameObject,root);copy.name="Master Slider";}
        SliderRow(root,"Master Slider",.66f,out settings.MasterSlider,out settings.MasterPercentText);
        SliderRow(root,"SFX Slider",.47f,out settings.SfxSlider,out settings.SfxPercentText);
        SliderRow(root,"Music Slider",.28f,out settings.MusicSlider,out settings.MusicPercentText);
        Area(settings.ShowTipsToggle.transform,.64f,.67f,.9f,.77f);
        LabelStyle(settings.ShowTipsToggle.GetComponentInChildren<TextMeshProUGUI>(),"Show tips",34);
        foreach(var toggle in new[]{settings.ShowTipsToggle,root.Find("Particles")!=null?root.Find("Particles").GetComponent<UnityEngine.UI.Toggle>():null})
        {
            if(toggle==null)continue;
            var hitArea=toggle.GetComponent<UnityEngine.UI.Image>()??toggle.gameObject.AddComponent<UnityEngine.UI.Image>();
            hitArea.color=Color.clear;hitArea.raycastTarget=true;
        }
        Area(settings.ShowTipsToggle.transform.Find("Box"),0,.25f,.11f,.75f);
        Area(settings.ShowTipsToggle.transform.Find("Label"),.15f,0,1,1);
        if(root.Find("Particles")==null){var copy=UnityEngine.Object.Instantiate(settings.ShowTipsToggle.gameObject,root);copy.name="Particles";}
        settings.ParticlesToggle=root.Find("Particles").GetComponent<UnityEngine.UI.Toggle>();settings.ParticlesToggle.onValueChanged=new UnityEngine.UI.Toggle.ToggleEvent();
        Area(settings.ParticlesToggle.transform,.64f,.53f,.9f,.63f);LabelStyle(settings.ParticlesToggle.GetComponentInChildren<TextMeshProUGUI>(),"Particles",34);
        Button(root,"KeybindsButton","KEYBINDS",.64f,.35f,.9f,.44f,settings.OpenKeybinds);
        Button(root,"ResetDataButton","RESET DATA",.57f,.17f,.80f,.25f,settings.ShowResetConfirmation);
        Button(root,"FullScreen","FULL SCREEN",.78f,.05f,.97f,.12f);
        var dev=root.Find("Dev Pannel");Area(dev,.035f,.045f,.39f,.21f);
        dev.GetComponent<UnityEngine.UI.Image>().color=new Color(1,1,1,.6f);
        Label(dev,"CodesLabel","CODES",24,.03f,.66f,.45f,.98f);
        Area(dev.Find("CodeInput"),.03f,.3f,.55f,.68f);Area(dev.Find("SubmitButton"),.61f,.3f,.98f,.68f);
        LabelStyle(dev.Find("SubmitButton").GetComponentInChildren<TextMeshProUGUI>(),"APPLY",24);
        Area(dev.Find("ResultText"),.02f,0,.98f,.27f);LabelStyle(dev.Find("ResultText").GetComponent<TextMeshProUGUI>(),"",20);
        var input=dev.Find("CodeInput").GetComponent<TMP_InputField>();
        input.lineType=TMP_InputField.LineType.SingleLine;input.pointSize=26;input.richText=false;
        Area(input.textViewport,.03f,.06f,.97f,.94f);
        foreach(var label in input.GetComponentsInChildren<TextMeshProUGUI>())
        {
            Area(label.transform,0,0,1,1);LabelStyle(label,label.name=="Placeholder"?"4-digit code":"",26);
            label.fontStyle=FontStyles.Normal;label.enableAutoSizing=false;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.fontSharedMaterial=_font.material;label.extraPadding=false;label.UpdateMeshPadding();label.ForceMeshUpdate(true,true);
        }
        BuildKeybinds(settings);
        var reset=Panel(root,"ResetConfirmation",new Color(.06f,.10f,.16f,.96f)); settings.ResetConfirmationPanel=reset.gameObject;
        ModalGroup(reset.gameObject);
        var message=Label(reset.transform,"Message","RESET ALL PROGRESS?\n\nCoins, cards, essence, developer resources and tip history will reset.\nAudio, controls and difficulty settings are kept.\n\nThis cannot be undone.",40,.18f,.36f,.82f,.80f);message.color=Color.white;
        Button(reset.transform,"CancelReset","CANCEL",.22f,.20f,.45f,.30f,settings.CancelReset);
        Button(reset.transform,"ConfirmReset","RESET PROGRESS",.55f,.20f,.78f,.30f,settings.ConfirmReset);
        reset.gameObject.SetActive(false);EditorUtility.SetDirty(settings);
    }
    static void ModalGroup(GameObject panel)
    {
        var group=panel.GetComponent<CanvasGroup>();
        if(group==null)group=panel.AddComponent<CanvasGroup>();
        group.ignoreParentGroups=true;group.interactable=true;group.blocksRaycasts=true;
    }
    static void BuildKeybinds(SettingsMenuUI settings)
    {
        var panel=Panel(settings.transform,"KeybindPanel",Paper);settings.KeybindPanel=panel.gameObject;ModalGroup(panel.gameObject);
        Label(panel.transform,"Title","KEYBINDS",48,.3f,.88f,.7f,.98f);
        Button(panel.transform,"BackButton","BACK",.035f,.87f,.20f,.95f,settings.CloseKeybinds);
        settings.KeybindLabels=new TextMeshProUGUI[SettingsMenuUI.BindingActions.Length];
        for(int i=0;i<SettingsMenuUI.BindingActions.Length;i++)
        {
            int col=i<6?0:1;int row=i<6?i:i-6;float x=.065f+col*.49f;float y=.74f-row*.095f;
            Label(panel.transform,"Action"+i,SettingsMenuUI.BindingNames[i],30,x,y,x+.27f,y+.075f);
            var button=Button(panel.transform,"Bind"+i,"KEY",x+.28f,y,x+.43f,y+.075f);
            IntAction(button,settings.BeginBinding,i);settings.KeybindLabels[i]=button.GetComponentInChildren<TextMeshProUGUI>();
        }
        Button(panel.transform,"RestoreDefaults","RESTORE DEFAULTS",.60f,.23f,.91f,.32f,settings.RestoreKeybinds);
        settings.KeybindStatus=Label(panel.transform,"Status","Select a control to change it. Escape cancels binding.",26,.05f,.06f,.95f,.14f);
        var capture=Panel(panel.transform,"KeyCapture",new Color(.06f,.10f,.16f,.98f));settings.KeyCapturePanel=capture.gameObject;ModalGroup(capture.gameObject);
        settings.KeyCaptureText=Label(capture.transform,"Prompt","Press a key or mouse button\nEscape to cancel",46,.1f,.3f,.9f,.7f);settings.KeyCaptureText.color=Color.white;
        capture.gameObject.SetActive(false);panel.gameObject.SetActive(false);
    }
    static void ConfigureIndexCard(GameObject prefab)
    {
        string path=AssetDatabase.GetAssetPath(prefab);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var card=root.GetComponent<CardDisplay>();
            foreach(var label in new[]{card.ProgressText,card.LevelText,card.UpgradeCostText,card.EssenceText})
                if(label!=null){label.enableAutoSizing=true;label.fontSizeMin=26;label.fontSizeMax=34;label.fontSize=34;}
            if(card.UpgradeButton!=null)
            {
                ((RectTransform)card.UpgradeButton.transform).sizeDelta=new Vector2(180,90);
                foreach(var label in card.UpgradeButton.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    bool cost=label==card.UpgradeCostText;
                    Area(label.transform,.02f,cost?.02f:.48f,.98f,cost?.50f:.98f);
                    label.alignment=TextAlignmentOptions.Center;label.margin=Vector4.zero;
                    label.enableAutoSizing=true;label.fontSizeMin=20;label.fontSizeMax=28;label.fontSize=28;
                    label.textWrappingMode=TextWrappingModes.NoWrap;
                }
            }
            if(card.DescriptionText!=null){card.DescriptionText.enableAutoSizing=true;card.DescriptionText.fontSizeMin=24;card.DescriptionText.fontSizeMax=36;}
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void ConfigureParticles()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs/Effects"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentsInChildren<ParticleSystem>(true).Length==0) continue;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed=false;
                foreach(var particle in root.GetComponentsInChildren<ParticleSystem>(true))
                    if(particle.GetComponent<ParticleVisibility>()==null){particle.gameObject.AddComponent<ParticleVisibility>();changed=true;}
                if(changed) PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}

