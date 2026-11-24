using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MixMaster.Combat;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MixMaster.UI
{
    public sealed class SkillScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Dropdown actorDropdown,skillDropdown,triggerDropdown,andDropdown;
        [SerializeField] private TMP_InputField threshold,andThreshold,priority;
        [SerializeField] private TMP_Text details,message;
        [SerializeField] private ScrollRect detailsScroll;
        [SerializeField] private Button openButton,closeButton,learnButton,saveButton,clearButton;
        [SerializeField] private Button[] assignButtons,castButtons;
        [SerializeField] private TMP_Text[] assignLabels,castLabels;
        [Header("選択・習得済みの表示色")]
        [SerializeField] private Color selectedSlotColor = new Color(.15f,.48f,.66f,1f);
        [SerializeField] private Color learnedSkillColor = new Color(.45f,1f,.65f,1f);
        private sealed class SkillOwner
        {
            public string OwnerId, Name;
            public ElementType? Element;
            public bool IsPlayer => OwnerId == "player";
        }
        private readonly List<SkillOwner> actors=new List<SkillOwner>();
        private readonly List<SkillSO> options=new List<SkillSO>();
        private SkillProgressionManager manager;
        private MonsterManager monsters;
        private Color[] originalSlotColors;
        private bool initialized;
        private string selectedOwnerId = "player";
        private SkillOwner individualOwner;
        private bool individualSelection;
        private string partySnapshot;
        [SerializeField] private Button autoSetupButton;
        private ProgressionPanelUI autoSetupPanel;
        private int slotIndex;
        private float refreshAt;
        private (SkillSO Skill, string OwnerId, string IndividualName, ElementType? Element, bool Learned,
            int Points, int Revision, int LoadoutRevision) renderedDetails;
        private static readonly string[] Triggers={"手動のみ／追加条件なし","使用可能になったら","自分の体力％以下","自分のMP％以上","対象の体力％以下","攻撃範囲内の敵の数以上","通常攻撃が指定回数命中","指定回数ダメージを受けたら","指定秒数ごと","指定数の敵を倒したら"};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;EnsureScreen();
        }
        private static void SceneLoaded(Scene scene,LoadSceneMode mode)=>EnsureScreen();
        private static SkillScreenUI EnsureScreen()
        {
            var existing=FindFirstObjectByType<SkillScreenUI>(FindObjectsInactive.Include);
            if(existing!=null)return existing;
            Canvas canvas=null;foreach(var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(c.isRootCanvas&&c.renderMode!=RenderMode.WorldSpace){canvas=c;break;}if(canvas==null)return null;
            var prefab=Resources.Load<GameObject>("Skills/SkillHUD");
            return prefab!=null?Instantiate(prefab,canvas.transform,false).GetComponent<SkillScreenUI>():Build(canvas.transform);
        }
        // Detailed views use the owned ID, independent of a spawned combat actor.
        public static bool OpenForOwner(string ownerId)
        {
            var ui=EnsureScreen();if(ui==null)return false;
            ui.Initialize();if(!ui.BindManager())return false;
            if(ownerId!="player"&&(string.IsNullOrEmpty(ownerId)||ui.monsters?.FindOwnedMonster(ownerId)==null))return false;
            ui.gameObject.SetActive(true);ui.selectedOwnerId=ownerId;ui.individualSelection=ownerId!="player";ui.slotIndex=0;
            ui.Refresh();ui.transform.SetAsLastSibling();ui.panel.SetActive(true);ui.panel.transform.SetAsLastSibling();
            return true;
        }
        private void Awake()=>Initialize();
        private void Start()=>Initialize();
        private void Initialize()
        {
            // Build() adds this component before assigning its UI references.
            if(initialized||panel==null)return;initialized=true;
            EnsureDetailsScroll();
            if (autoSetupButton == null)
                autoSetupButton = InventoryUiFactory.Button(panel.transform, "AutoSetupSkills", "スキル一括設定",
                    new Vector2(24, -1425), new Vector2(880, 42));
            autoSetupButton.GetComponentInChildren<TMP_Text>(true).font = message.font;
            autoSetupButton.onClick.AddListener(OpenAutoSetup);
            openButton.onClick.AddListener(()=>{panel.SetActive(!panel.activeSelf);if(panel.activeSelf){individualSelection=false;individualOwner=null;transform.SetAsLastSibling();panel.transform.SetAsLastSibling();Refresh();}});
            closeButton.onClick.AddListener(()=>{panel.SetActive(false);if(autoSetupPanel!=null)autoSetupPanel.Close();});
            actorDropdown.onValueChanged.AddListener(_=>{individualSelection=false;individualOwner=null;selectedOwnerId=Owner?.OwnerId??"player";slotIndex=0;RefreshSkills();});
            skillDropdown.onValueChanged.AddListener(_=>RefreshDetails());
            learnButton.onClick.AddListener(()=>{if(Owner==null||Selected==null)return;manager.Learn(Owner.OwnerId,Selected.skillId,out string text);message.text=text;RefreshSkills();});
            for(int i=0;i<assignButtons.Length;i++){int index=i;assignButtons[i].onClick.AddListener(()=>SelectSlot(index));}
            for(int i=0;i<castButtons.Length;i++){int index=i;castButtons[i].onClick.AddListener(()=>Cast(index));}
            saveButton.onClick.AddListener(SaveRule);clearButton.onClick.AddListener(()=>{if(Owner!=null)manager.SetSlot(Owner.OwnerId,slotIndex,new SkillSlot());Refresh();});
            originalSlotColors=new Color[assignButtons.Length];
            for(int i=0;i<assignButtons.Length;i++)originalSlotColors[i]=assignButtons[i].targetGraphic!=null?assignButtons[i].targetGraphic.color:Color.white;
            if(skillDropdown.captionText!=null)skillDropdown.captionText.richText=true;
            if(skillDropdown.itemText!=null)skillDropdown.itemText.richText=true;
            panel.SetActive(false);
        }
        private bool BindManager()
        {
            var current=SkillProgressionManager.Instance;
            if(current!=manager)
            {
                if(manager!=null)manager.Changed-=Refresh;
                manager=current;if(manager!=null)manager.Changed+=Refresh;
                renderedDetails = default;
            }
            if(manager==null)return false;
            monsters=manager.GetComponent<MonsterManager>();return true;
        }
        private void OnDestroy(){if(manager!=null)manager.Changed-=Refresh;}
        private SkillOwner Owner=>individualOwner??(actorDropdown.value>=0&&actorDropdown.value<actors.Count?actors[actorDropdown.value]:null);
        private SkillSO Selected=>skillDropdown.value>=0&&skillDropdown.value<options.Count?options[skillDropdown.value]:null;
        private void Update()
        {
            if(!initialized)Initialize();if(!initialized||!BindManager())return;
            if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+0.2f;
            string signature=monsters!=null?string.Join("|",monsters.PartyMonsterUniqueIds):"";
            if(signature!=partySnapshot)Refresh();
            if(individualOwner!=null&&monsters?.FindOwnedMonster(individualOwner.OwnerId)==null){individualSelection=false;Refresh();}
            if(panel.activeSelf)RefreshDetails();
            var player=SkillActor.PlayerActor;
            for(int i=0;i<castButtons.Length;i++)
            {
                var slot=manager.Loadout("player").slots[i];var skill=manager.Catalog!=null?manager.Catalog.Get(slot.skillId):null;
                float cd=player!=null?player.Remaining(skill):0;
                castLabels[i].text=skill==null?"未登録":skill.displayName+(cd>0?"\n"+cd.ToString("0.0")+"秒":"");
                castButtons[i].interactable=skill!=null&&skill.category==SkillCategory.Active&&player!=null&&player.Alive&&cd<=0&&player.Mp>=skill.mpCost&&!player.SkillBlocked;
            }
        }
        private void Cast(int index)
        {
            if(manager==null||SkillActor.PlayerActor==null)return;
            var s=manager.Catalog!=null?manager.Catalog.Get(manager.Loadout("player").slots[index].skillId):null;
            SkillActor.PlayerActor.TryCast(s,out string text);message.text=text;
        }
        public void Refresh()
        {
            if(!BindManager())return;
            actors.Clear();actors.Add(new SkillOwner{OwnerId="player",Name="主人公"});
            var catalog=MonsterCatalogSO.Load();
            if(monsters!=null)foreach(var id in monsters.PartyMonsterUniqueIds)
            {
                var record=monsters.FindOwnedMonster(id);
                if(record==null||string.IsNullOrEmpty(record.uniqueId))continue;
                actors.Add(DescribeOwner(record,catalog));
            }
            partySnapshot=monsters!=null?string.Join("|",monsters.PartyMonsterUniqueIds):"";
            var names=new List<string>();foreach(var a in actors)names.Add(a.Name);
            actorDropdown.ClearOptions();actorDropdown.AddOptions(names);
            int index=actors.FindIndex(a=>a.OwnerId==selectedOwnerId);
            individualOwner=null;
            if(index<0&&individualSelection)
            {
                var record=monsters?.FindOwnedMonster(selectedOwnerId);
                if(record!=null)individualOwner=DescribeOwner(record,catalog);
            }
            index=Mathf.Max(0,index);actorDropdown.SetValueWithoutNotify(index);
            // A reserve opened from its detail has its own context, never a dropdown entry.
            actorDropdown.gameObject.SetActive(individualOwner==null);
            if(individualOwner==null)selectedOwnerId=actors[index].OwnerId;
            RefreshSkills();
        }
        private static SkillOwner DescribeOwner(OwnedMonsterRecord record,MonsterCatalogSO catalog)
        {
            var species=catalog!=null?catalog.GetMonster(record.monsterId):null;
            return new SkillOwner
            {
                OwnerId = record.uniqueId,
                Name = "仲間：" + (species != null ? species.displayName : record.monsterId) + " Lv." + record.level,
                Element = species != null ? species.baseStats?.element : null
            };
        }
        private string SkillLabel(SkillSO skill)
        {
            return Owner!=null&&manager.Learned(Owner.OwnerId,skill.skillId)
                ? "<color=#"+ColorUtility.ToHtmlStringRGB(learnedSkillColor)+">"+skill.displayName+"（習得済み）</color>" : skill.displayName;
        }
        private void RefreshSkills()
        {
            string id=Selected!=null?Selected.skillId:null;options.Clear();var names=new List<string>();
            if(Owner!=null)foreach(var s in manager.Available(Owner.OwnerId))if(s!=null&&s.executable){options.Add(s);names.Add(SkillLabel(s));}
            skillDropdown.ClearOptions();skillDropdown.AddOptions(names);int index=options.FindIndex(s=>s.skillId==id);skillDropdown.SetValueWithoutNotify(Mathf.Max(0,index));
            SelectSlot(slotIndex);RefreshDetails();
        }
        private void SelectSlot(int i)
        {
            slotIndex=Mathf.Clamp(i,0,SkillProgressionManager.SlotCount-1);if(Owner==null)return;var slots=manager.Loadout(Owner.OwnerId).slots;
            for(int n=0;n<assignButtons.Length;n++)
            {
                var skill=manager.Catalog?.Get(slots[n].skillId);assignLabels[n].text=(n==slotIndex?"選択中 ":"")+(n+1)+"\n"+(skill!=null?skill.displayName:"未登録");
                if(assignButtons[n].targetGraphic!=null&&originalSlotColors!=null)
                    assignButtons[n].targetGraphic.color=n==slotIndex?selectedSlotColor:originalSlotColors[n];
            }
            var choices=new List<string>(Triggers);int offset=Owner.IsPlayer?0:1;
            if(offset==1)choices.RemoveAt(0);triggerDropdown.ClearOptions();triggerDropdown.AddOptions(choices);
            var slot=slots[slotIndex];triggerDropdown.SetValueWithoutNotify(Mathf.Max(0,(int)slot.trigger-offset));andDropdown.SetValueWithoutNotify((int)slot.andCondition);
            threshold.text=slot.threshold.ToString(CultureInfo.InvariantCulture);andThreshold.text=slot.andThreshold.ToString(CultureInfo.InvariantCulture);priority.text=slot.priority.ToString(CultureInfo.InvariantCulture);
        }
        private void RefreshDetails()
        {
            var owner = Owner;
            var skill = Selected;
            bool active = owner != null && skill != null && skill.category == SkillCategory.Active;
            triggerDropdown.interactable=andDropdown.interactable=threshold.interactable=andThreshold.interactable=priority.interactable=active;
            if (owner == null || skill == null)
            {
                const string empty = "使用可能なスキルがありません。スキル表・転生先の習得可能一覧を確認してください。";
                if (details.text != empty) SetDetailsText(empty, true);
                renderedDetails = default;
                learnButton.interactable = saveButton.interactable = false;
                return;
            }

            bool learned = manager.Learned(owner.OwnerId, skill.skillId);
            learnButton.interactable = !learned;
            saveButton.interactable = learned;

            ElementType? element = null;
            if (active && skill.useOwnerElement)
                element = owner.IsPlayer ? manager.GetComponent<PlayerManager>()?.UnmodifiedStats?.element : owner.Element;
            int points = manager.Points(owner.OwnerId);
            var loadout = manager.Loadout(owner.OwnerId);
            var state = (skill, owner.OwnerId, individualOwner?.Name, element, learned, points, manager.Revision, loadout.revision);
            if (state == renderedDetails) return;

            int registered = loadout.slots.FindIndex(slot => slot != null && slot.skillId == skill.skillId);
            var text = new StringBuilder();
            if (individualOwner != null) text.Append(individualOwner.Name).Append('\n');
            text.Append("<b>").Append(EncyclopediaContent.Name(skill.displayName, skill.skillId)).Append("</b>\n")
                .Append(learned ? "習得済み" : "未習得").Append("　所持SP ").Append(points.ToString("N0", CultureInfo.InvariantCulture));
            if (registered >= 0) text.Append("　枠 ").Append(registered + 1).Append(" に登録中");
            text.Append('\n').Append(EncyclopediaContent.SkillDetails(skill, element));
            if (active && registered >= 0)
            {
                var slot = loadout.slots[registered];
                text.Append("\n\n<b>保存済みの発動条件</b>\n").Append(DescribeTrigger(slot.trigger, slot.threshold));
                if (slot.trigger != SkillTrigger.Manual && slot.andCondition != SkillTrigger.Manual && slot.andCondition != SkillTrigger.Ready)
                    text.Append("\nかつ ").Append(DescribeTrigger(slot.andCondition, slot.andThreshold));
            }

            bool selectionChanged = renderedDetails.Skill != skill || renderedDetails.OwnerId != owner.OwnerId;
            SetDetailsText(text.ToString(), selectionChanged);
            renderedDetails = state;
        }

        private static string DescribeTrigger(SkillTrigger trigger, float threshold)
        {
            string ratio = threshold.ToString("0.##", CultureInfo.InvariantCulture) + "%";
            string count = Math.Ceiling(Math.Max(1f, threshold)).ToString("N0", CultureInfo.InvariantCulture);
            return trigger switch
            {
                SkillTrigger.Manual => "手動のみ",
                SkillTrigger.Ready => "使用可能になったら自動発動",
                SkillTrigger.SelfHpBelow => "自分の残りHPが " + ratio + " 以下",
                SkillTrigger.SelfMpAbove => "自分の残りMPが " + ratio + " 以上",
                SkillTrigger.TargetHpBelow => "対象の残りHPが " + ratio + " 以下",
                SkillTrigger.EnemyCount => "通常攻撃範囲内の敵が " + count + " 体以上",
                SkillTrigger.NormalHits => "通常攻撃が " + count + " 回命中",
                SkillTrigger.DamageTaken => "通常攻撃で " + count + " 回ダメージを受けたら",
                SkillTrigger.Interval => Math.Max(.2f, threshold).ToString("0.##", CultureInfo.InvariantCulture) + "秒ごと",
                SkillTrigger.Kill => "通常攻撃で敵を " + count + " 体倒したら",
                _ => "未設定"
            };
        }

        private void SetDetailsText(string text, bool resetScroll)
        {
            details.richText = true;
            details.text = text;
            if (!resetScroll || detailsScroll == null || detailsScroll.content == null) return;
            detailsScroll.StopMovement();
            var content = detailsScroll.content;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
        }

        private void EnsureDetailsScroll()
        {
            if (detailsScroll != null || details == null) return;

            // Older scene copies may still have the text directly on the panel.
            var label = details.rectTransform;
            detailsScroll = CreateDetailsScroll(label.parent, "DetailsScroll");
            var view = detailsScroll.viewport;
            view.SetSiblingIndex(label.GetSiblingIndex());
            view.anchorMin = label.anchorMin;
            view.anchorMax = label.anchorMax;
            view.pivot = label.pivot;
            view.sizeDelta = label.sizeDelta;
            view.anchoredPosition3D = label.anchoredPosition3D;
            view.localRotation = label.localRotation;
            view.localScale = label.localScale;
            SetDetailsContent(detailsScroll, details);
        }

        private static ScrollRect CreateDetailsScroll(Transform parent, string name)
        {
            var view = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            view.layer = parent.gameObject.layer;
            view.transform.SetParent(parent, false);
            view.GetComponent<Image>().color = new Color(.035f, .06f, .092f, 1f);
            var scroll = view.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)view.transform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            return scroll;
        }

        private static void SetDetailsContent(ScrollRect scroll, TMP_Text label)
        {
            var rect = label.rectTransform;
            rect.SetParent(scroll.viewport, false);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableAutoSizing = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.margin = new Vector4(14f, 9f, 14f, ScrollViewLayout.DefaultEndPadding);
            label.raycastTarget = false;
            var fitter = label.GetComponent<ContentSizeFitter>() ?? label.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rect;
            ScrollViewLayout.Prepare(scroll);
        }
        private void OpenAutoSetup()
        {
            if (Owner == null || !BindManager()) return;
            if (autoSetupPanel == null) autoSetupPanel = ProgressionPanelUI.Create(panel.transform, "AutoSetupSkills");
            var rows = new List<ProgressionPanelUI.Item>
            {
                new ProgressionPanelUI.Item("攻撃重視", () => PreviewAutoSetup(SkillSetupProfile.Attack)),
                new ProgressionPanelUI.Item("回復優先", () => PreviewAutoSetup(SkillSetupProfile.Healing)),
                new ProgressionPanelUI.Item("便利系優先", () => PreviewAutoSetup(SkillSetupProfile.Utility))
            };
            autoSetupPanel.Show("スキル一括設定", rows, "選択中の個体の習得済みスキルから選びます。設定前に内容を確認できます。");
        }

        private void PreviewAutoSetup(SkillSetupProfile profile)
        {
            if (Owner == null) return;
            string owner = Owner.OwnerId;
            var slots = manager.PreviewAutoSetup(owner, profile);
            var rows = new List<ProgressionPanelUI.Item>();
            int equipped = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var skill = manager.Catalog?.Get(slot.skillId);
                if (skill != null) equipped++;
                rows.Add(new ProgressionPanelUI.Item($"枠 {i + 1}　" + (skill != null ? skill.displayName : "空き") +
                    (skill != null ? "\n" + (skill.category == SkillCategory.Passive ? "セット中に有効" : DescribeTrigger(slot.trigger, slot.threshold)) : "")));
            }
            autoSetupPanel.Show("一括設定の確認", rows, "現在の5枠と条件をこの内容に置き換えます。SPは消費しません。",
                equipped > 0 ? "この内容で設定" : null, () =>
                {
                    if (manager.ApplyAutoSetup(owner, slots, out string result))
                    {
                        FindFirstObjectByType<SaveManager>()?.SaveGame();
                        autoSetupPanel.Close();
                        Refresh();
                    }
                    message.text = result;
                }, "方針へ戻る", OpenAutoSetup);
        }

        private void SaveRule()
        {
            if(Owner==null||Selected==null)return;
            if(!float.TryParse(threshold.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float t)||!float.TryParse(andThreshold.text,NumberStyles.Float,CultureInfo.InvariantCulture,out float a)||!int.TryParse(priority.text,out int p)||float.IsNaN(t)||float.IsInfinity(t)||float.IsNaN(a)||float.IsInfinity(a)||t<0||a<0){message.text="条件の数値を確認してください。";return;}
            manager.SetSlot(Owner.OwnerId,slotIndex,new SkillSlot{skillId=Selected.skillId,trigger=(SkillTrigger)(triggerDropdown.value+(Owner.IsPlayer?0:1)),threshold=t,andCondition=(SkillTrigger)andDropdown.value,andThreshold=a,priority=p});
            message.text="登録しました。優先度は大きい枠から判定します。";SelectSlot(slotIndex);
        }
        public static SkillScreenUI Build(Transform parent)
        {
            var go=new GameObject("SkillHUD",typeof(RectTransform));go.transform.SetParent(parent,false);InventoryUiFactory.Stretch((RectTransform)go.transform);
            var ui=go.AddComponent<SkillScreenUI>();
            Transform content=go.transform;
            var dock=new GameObject("SkillButtons",typeof(RectTransform));dock.transform.SetParent(content,false);var dockRect=(RectTransform)dock.transform;dockRect.anchorMin=dockRect.anchorMax=new Vector2(1,0);dockRect.pivot=new Vector2(1,0);dockRect.anchoredPosition=new Vector2(-20,420);dockRect.sizeDelta=new Vector2(550,150);
            ui.castButtons=new Button[5];ui.castLabels=new TMP_Text[5];
            for(int i=0;i<5;i++){ui.castButtons[i]=InventoryUiFactory.Button(dock.transform,"Cast"+i,"未登録",new Vector2(i*110,0),new Vector2(104,80));ui.castLabels[i]=ui.castButtons[i].GetComponentInChildren<TMP_Text>();}
            ui.openButton=InventoryUiFactory.Button(dock.transform,"OpenSkills","スキル設定",new Vector2(360,-88),new Vector2(190,52));
            var panel=new GameObject("SkillPanel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(content,false);var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(940,1480);panel.GetComponent<Image>().color=new Color(.07f,.1f,.15f,.98f);ui.panel=panel;
            InventoryUiFactory.Text(rect,"Title","スキル習得・発動条件",new Vector2(24,-20),new Vector2(700,60),32);
            ui.closeButton=InventoryUiFactory.Button(rect,"Close","閉じる",new Vector2(750,-24),new Vector2(160,52));
            ui.actorDropdown=InventoryUiFactory.Dropdown(rect,"Actor",new Vector2(24,-100),new Vector2(880,58),new[]{"主人公"});
            ui.skillDropdown=InventoryUiFactory.Dropdown(rect,"Skill",new Vector2(24,-180),new Vector2(680,58),new[]{"スキルを選択"});
            ui.learnButton=InventoryUiFactory.Button(rect,"Learn","習得",new Vector2(720,-180),new Vector2(180,58));
            ui.detailsScroll = CreateDetailsScroll(rect, "Details");
            InventoryUiFactory.TopLeft(ui.detailsScroll.viewport, new Vector2(24, -265), new Vector2(880, 320));
            ui.details = InventoryUiFactory.Text(ui.detailsScroll.viewport, "Description", "", Vector2.zero, Vector2.zero, 25);
            SetDetailsContent(ui.detailsScroll, ui.details);
            ui.assignButtons=new Button[5];ui.assignLabels=new TMP_Text[5];
            for(int i=0;i<5;i++){ui.assignButtons[i]=InventoryUiFactory.Button(rect,"Slot"+i,"未登録",new Vector2(24+i*178,-600),new Vector2(168,105));ui.assignLabels[i]=ui.assignButtons[i].GetComponentInChildren<TMP_Text>();}
            InventoryUiFactory.Text(rect,"TriggerLabel","自動発動条件（手動のみなら自動発動しません）",new Vector2(24,-740),new Vector2(880,40),24);
            ui.triggerDropdown=InventoryUiFactory.Dropdown(rect,"Trigger",new Vector2(24,-800),new Vector2(680,60),Triggers);ui.threshold=Input(rect,"Threshold",new Vector2(730,-800),"50");
            InventoryUiFactory.Text(rect,"AndLabel","さらに満たす条件（両方成立したら発動）",new Vector2(24,-900),new Vector2(880,40),24);
            ui.andDropdown=InventoryUiFactory.Dropdown(rect,"And",new Vector2(24,-960),new Vector2(680,60),Triggers);ui.andThreshold=Input(rect,"AndThreshold",new Vector2(730,-960),"50");
            InventoryUiFactory.Text(rect,"PriorityLabel","優先度（大きい順）",new Vector2(24,-1070),new Vector2(500,50),24);ui.priority=Input(rect,"Priority",new Vector2(730,-1070),"0");
            ui.saveButton=InventoryUiFactory.Button(rect,"Save","選択した枠に登録・条件を保存",new Vector2(24,-1170),new Vector2(660,65));ui.clearButton=InventoryUiFactory.Button(rect,"Clear","枠を解除",new Vector2(710,-1170),new Vector2(190,65));
            ui.message=InventoryUiFactory.Text(rect,"Message","割合は0〜100、回数・敵数は1以上、間隔は秒で入力。\n回復は射程内で体力割合が最も低い味方を選びます。",new Vector2(24,-1280),new Vector2(880,150),23);
            panel.SetActive(false);return ui;
        }
        private static TMP_InputField Input(Transform parent,string name,Vector2 position,string initial)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(TMP_InputField));go.transform.SetParent(parent,false);InventoryUiFactory.TopLeft((RectTransform)go.transform,position,new Vector2(170,60));go.GetComponent<Image>().color=new Color(.2f,.26f,.34f);
            var text=InventoryUiFactory.Text(go.transform,"Value",initial,new Vector2(8,-5),new Vector2(154,50),24);var input=go.GetComponent<TMP_InputField>();input.textViewport=(RectTransform)go.transform;input.textComponent=(TextMeshProUGUI)text;input.text=initial;return input;
        }
    }
}

