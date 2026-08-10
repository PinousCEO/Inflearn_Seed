using System;
using System.Collections.Generic;
using System.Linq;
using IdleBattle;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace IdleBattle.Editor
{
    public static class PassiveTreePrefabBuilder
    {
        private const string PrefabPath = "Assets/01_Prefabs/UI/PassiveSkillTree.prefab";
        private const string FrameSheet = "Assets/05_Resources/UI/Main/MainFrames-Layered-v3-SpriteSheet.png";
        private const string PanelSpritePath = "Assets/05_Resources/UI/Frames/Img_Frame_Panel_Gold.png";
        private const string ButtonSpritePath = "Assets/05_Resources/UI/Frames/Img_Btn_Primary_Normal.png";

        private readonly struct NodeSpec
        {
            public readonly string id, title, description;
            public readonly Vector2 position;
            public readonly PassiveEffectType effect;
            public readonly float value;
            public readonly bool major;
            public readonly string[] parents;

            public NodeSpec(string id, string title, string description, float x, float y,
                PassiveEffectType effect, float value, bool major, params string[] parents)
            {
                this.id = id;
                this.title = title;
                this.description = description;
                position = new Vector2(x, y);
                this.effect = effect;
                this.value = value;
                this.major = major;
                this.parents = parents;
            }
        }

        private static readonly NodeSpec[] Specs =
        {
            new("origin","Path of the Warlord","The first step of every barbarian.",0,-40,PassiveEffectType.Strength,2,true),
            new("might_1","Raw Might","Increase physical strength.",-150,130,PassiveEffectType.Strength,3,false,"origin"),
            new("guard_1","Thick Hide","Increase armor.",150,130,PassiveEffectType.Armor,4,false,"origin"),
            new("fury_1","Rising Fury","Attack faster after engaging.",-300,300,PassiveEffectType.AttackSpeed,2,false,"might_1"),
            new("blood_1","Blood Memory","Gain maximum vitality.",0,330,PassiveEffectType.Vitality,5,false,"might_1","guard_1"),
            new("iron_1","Iron Stance","Stand firm against heavy blows.",300,300,PassiveEffectType.Armor,6,false,"guard_1"),
            new("cleave_1","Wide Cleave","Increase area damage.",-430,490,PassiveEffectType.AreaDamage,5,false,"fury_1"),
            new("crit_1","Keen Edge","Increase critical chance.",-210,520,PassiveEffectType.CriticalChance,3,false,"fury_1","blood_1"),
            new("leech_1","Taste of Blood","Recover life through damage.",70,520,PassiveEffectType.LifeSteal,2,false,"blood_1"),
            new("mana_1","War Spirit","Increase maximum mana.",330,500,PassiveEffectType.MaxMana,8,false,"iron_1"),
            new("berserker","Berserker's Oath","Major: trade restraint for relentless offense.",-350,710,PassiveEffectType.AttackSpeed,8,true,"cleave_1","crit_1"),
            new("execution","Executioner's Rhythm","Increase critical damage.",-80,720,PassiveEffectType.CriticalDamage,10,false,"crit_1"),
            new("survivor","Undying March","Major: greatly increase vitality.",190,710,PassiveEffectType.Vitality,14,true,"leech_1","mana_1"),
            new("focus_1","Battle Focus","Recover skill cooldown faster.",410,690,PassiveEffectType.CooldownRecovery,4,false,"mana_1"),
            new("storm_1","Whirlwind Path","Increase area and attack speed.",-470,920,PassiveEffectType.AreaDamage,8,false,"berserker"),
            new("rage_1","Endless Rage","Increase strength.",-230,930,PassiveEffectType.Strength,8,false,"berserker","execution"),
            new("skull_1","Skull Splitter","Increase critical damage.",20,930,PassiveEffectType.CriticalDamage,14,false,"execution"),
            new("heart_1","Giant's Heart","Increase vitality.",250,930,PassiveEffectType.Vitality,10,false,"survivor"),
            new("spirit_1","Ancestral Spirit","Increase mana and cooldown recovery.",450,900,PassiveEffectType.MaxMana,15,false,"focus_1"),
            new("maelstrom","Maelstrom","Major: devastating wide attacks.",-350,1150,PassiveEffectType.AreaDamage,16,true,"storm_1","rage_1"),
            new("slaughter","Slaughter","Major: brutal critical strikes.",-60,1160,PassiveEffectType.CriticalDamage,24,true,"rage_1","skull_1"),
            new("juggernaut","Juggernaut","Major: armor and vitality path.",230,1150,PassiveEffectType.Armor,20,true,"heart_1"),
            new("ancestor","Ancestral Wrath","Major: skills recover much faster.",440,1120,PassiveEffectType.CooldownRecovery,12,true,"spirit_1"),
            new("apex","Apex Predator","Final keystone of the Warlord.",40,1390,PassiveEffectType.Strength,20,true,"maelstrom","slaughter","juggernaut","ancestor")
        };

        [InitializeOnLoadMethod]
        private static void BuildOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                    Build();
            };
        }

        [MenuItem("Tools/Idle Battle/Rebuild Passive Skill Tree Prefab")]
        public static void Build()
        {
            EnsureFolder("Assets/01_Prefabs/UI");
            var root = UI("PassiveSkillTreeScreen [Place Under Main Canvas]", null, Vector2.zero, new Vector2(1080, 1920));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.AddComponent<CanvasGroup>();
            var controller = root.AddComponent<PassiveTreeController>();

            Image("Backdrop", root.transform, new Color(.018f,.022f,.018f,1), Vector2.zero, new Vector2(1080,1920));
            var header = Image("Header", root.transform, new Color(.07f,.065f,.05f,.98f), new Vector2(0,850), new Vector2(1020,150), LoadSprite(PanelSpritePath));
            Text("Title", header.transform, "WARLORD CONSTELLATION", 34, new Vector2(0,26), new Vector2(700,50), new Color(1,.83f,.45f,1), FontStyles.Bold);
            var level = Text("Level", header.transform, "LEVEL 1", 22, new Vector2(-350,-34), new Vector2(220,40), Color.white);
            var points = Text("Passive Points", header.transform, "PASSIVE POINTS  0", 22, new Vector2(290,-34), new Vector2(300,40), new Color(.65f,1,.4f,1), FontStyles.Bold);

            var viewport = UI("Tree Viewport", root.transform, new Vector2(0,20), new Vector2(1020,1500));
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(.025f,.03f,.024f,.98f);
            viewport.AddComponent<RectMask2D>();
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = true; scroll.inertia = true; scroll.decelerationRate = .12f;
            var content = UI("Node Content [Drag + Zoom Ready]", viewport.transform, new Vector2(0,-520), new Vector2(2100,3100));
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content.GetComponent<RectTransform>();

            var lineRoot = UI("Connections", content.transform, Vector2.zero, new Vector2(2100,3100));
            var nodeRoot = UI("Nodes", content.transform, Vector2.zero, new Vector2(2100,3100));
            var nodes = new Dictionary<string, PassiveTreeNodeView>();
            foreach (var spec in Specs)
                nodes[spec.id] = CreateNode(nodeRoot.transform, spec);
            foreach (var spec in Specs)
                foreach (var parent in spec.parents)
                    CreateConnection(lineRoot.transform, nodes[parent], nodes[spec.id], parent, spec.id);

            var detail = Image("Selected Node Panel", root.transform, new Color(.055f,.05f,.04f,.99f), new Vector2(0,-855), new Vector2(1020,250), LoadSprite(PanelSpritePath));
            var selectedName = Text("Node Name", detail.transform, "Select a node", 28, new Vector2(-260,70), new Vector2(430,44), new Color(1,.78f,.35f,1), FontStyles.Bold);
            var selectedDesc = Text("Description", detail.transform, string.Empty, 19, new Vector2(-250,5), new Vector2(450,80), new Color(.86f,.84f,.76f,1));
            var selectedEffect = Text("Effect", detail.transform, string.Empty, 21, new Vector2(-250,-67), new Vector2(450,42), new Color(.55f,1,.45f,1), FontStyles.Bold);
            var invest = Button("Invest Button", detail.transform, "INVEST", new Vector2(300,42), new Vector2(260,64));
            var refund = Button("Refund Button", detail.transform, "REFUND", new Vector2(300,-48), new Vector2(260,56));

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("nodeContent").objectReferenceValue = content.GetComponent<RectTransform>();
            serialized.FindProperty("levelText").objectReferenceValue = level;
            serialized.FindProperty("pointText").objectReferenceValue = points;
            serialized.FindProperty("selectedNameText").objectReferenceValue = selectedName;
            serialized.FindProperty("selectedDescriptionText").objectReferenceValue = selectedDesc;
            serialized.FindProperty("selectedEffectText").objectReferenceValue = selectedEffect;
            serialized.FindProperty("investButton").objectReferenceValue = invest;
            serialized.FindProperty("refundButton").objectReferenceValue = refund;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created detailed passive tree prefab: {PrefabPath}");
        }

        private static PassiveTreeNodeView CreateNode(Transform parent, NodeSpec spec)
        {
            var size = spec.major ? new Vector2(142,142) : new Vector2(104,104);
            var go = UI($"{spec.id} [{spec.title}]", parent, spec.position, size);
            var image = go.AddComponent<Image>();
            image.sprite = LoadSprite(FrameSheet, spec.major ? "MainFrames-Layered-v3-SpriteSheet_5" : "MainFrames-Layered-v3-SpriteSheet_6");
            image.preserveAspect = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var title = Text("Label", go.transform, spec.title, spec.major ? 17 : 14,
                new Vector2(0,-size.y*.68f), new Vector2(190,48), new Color(.95f,.86f,.64f,1),
                spec.major ? FontStyles.Bold : FontStyles.Normal);
            title.textWrappingMode = TextWrappingModes.Normal;
            var rank = Text("Rank", go.transform, "0/1", 12, new Vector2(0,0), new Vector2(52,24), Color.white, FontStyles.Bold);
            var view = go.AddComponent<PassiveTreeNodeView>();
            var so = new SerializedObject(view);
            so.FindProperty("nodeId").stringValue = spec.id;
            so.FindProperty("displayName").stringValue = spec.title;
            so.FindProperty("description").stringValue = spec.description;
            so.FindProperty("effectType").enumValueIndex = (int)spec.effect;
            so.FindProperty("effectValue").floatValue = spec.value;
            so.FindProperty("pointCost").intValue = spec.major ? 2 : 1;
            so.FindProperty("majorNode").boolValue = spec.major;
            var parents = so.FindProperty("prerequisiteIds");
            parents.arraySize = spec.parents.Length;
            for (var i=0;i<spec.parents.Length;i++) parents.GetArrayElementAtIndex(i).stringValue=spec.parents[i];
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("frame").objectReferenceValue = image;
            so.FindProperty("rankText").objectReferenceValue = rank;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static void CreateConnection(Transform parent, PassiveTreeNodeView from, PassiveTreeNodeView to, string sourceId, string targetId)
        {
            var a = ((RectTransform)from.transform).anchoredPosition;
            var b = ((RectTransform)to.transform).anchoredPosition;
            var delta = b-a;
            var go = UI($"{sourceId} -> {targetId}", parent, (a+b)*.5f, new Vector2(delta.magnitude, 9));
            go.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            var image = go.AddComponent<Image>();
            image.color = new Color(.2f,.2f,.22f,.72f);
            var view = go.AddComponent<PassiveTreeConnectionView>();
            var so = new SerializedObject(view);
            so.FindProperty("sourceId").stringValue=sourceId;
            so.FindProperty("targetId").stringValue=targetId;
            so.FindProperty("line").objectReferenceValue=image;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject UI(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.anchoredPosition=pos; rect.sizeDelta=size;
            return go;
        }

        private static GameObject Image(string name, Transform parent, Color color, Vector2 pos, Vector2 size, Sprite sprite=null)
        {
            var go=UI(name,parent,pos,size); var image=go.AddComponent<Image>();
            image.color=color;
            image.sprite=sprite;
            if(sprite!=null) image.type=UnityEngine.UI.Image.Type.Sliced;
            return go;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Vector2 pos, Vector2 area, Color color, FontStyles style=FontStyles.Normal)
        {
            var go=UI(name,parent,pos,area); var text=go.AddComponent<TextMeshProUGUI>();
            text.text=value; text.font=TMP_Settings.defaultFontAsset; text.fontSize=size; text.color=color;
            text.fontStyle=style; text.alignment=TextAlignmentOptions.Center; text.raycastTarget=false;
            return text;
        }

        private static Button Button(string name, Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var go=Image(name,parent,Color.white,pos,size,LoadSprite(ButtonSpritePath)); var button=go.AddComponent<Button>();
            button.targetGraphic=go.GetComponent<Image>(); Text("Label",go.transform,label,20,Vector2.zero,size,Color.white,FontStyles.Bold);
            return button;
        }

        private static Sprite LoadSprite(string path, string name=null)
        {
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
            return name==null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(value=>value.name==name);
        }

        private static void EnsureFolder(string path)
        {
            var parts=path.Split('/');
            var current=parts[0];
            for(var i=1;i<parts.Length;i++)
            {
                var next=$"{current}/{parts[i]}";
                if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]);
                current=next;
            }
        }
    }
}
