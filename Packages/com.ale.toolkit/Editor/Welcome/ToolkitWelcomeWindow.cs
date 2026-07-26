using System;
using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

#if ATK_TMP
using TMPro;
#endif

#if ATK_TMP && ATK_LOCALIZATION
using Ale.Toolkit.Runtime.UI;
#endif

namespace Ale.Toolkit.Editor
{
    /// <summary>
    /// Ale Toolkit 欢迎 / 全局设置窗口。承载<b>项目级全局设定</b>：编辑器界面语言（中 / 英 / 日）、
    /// 可选依赖宏开关（TextMeshPro / Unity Localization / Unity Addressables），以及通用工具窗口入口与文档。
    /// 上层插件（如库存）的领域功能各自在其欢迎窗口，全局设定统一跳到本窗口。菜单 Tools &gt; Ale Toolkit &gt; Welcome 打开。
    /// </summary>
    public class ToolkitWelcomeWindow : EditorWindow
    {
        private static readonly Vector2 MinWindowSize = new Vector2(460f, 600f);

        /// <summary>
        /// 打开「通用 Addressable 迁移窗口」的注入钩子。由受 ATK_ADDRESSABLE 约束的 Addressable 编辑器程序集
        /// 在 <c>[InitializeOnLoad]</c> 时赋值；为 null（宏未启用 / 包未装）时对应按钮自动隐藏。core 编辑器程序集
        /// 对 Addressables 零依赖，故不能直接引用迁移窗口类型，遂以此钩子解耦。
        /// </summary>
        public static Action OpenAddressableMigration;

        private bool _tmpEnabled,  _tmpInstalled;
        private bool _locEnabled,  _locInstalled;
        private bool _addrEnabled, _addrInstalled;
        private bool _initialized;
        private bool _pendingRecompile;
        private Vector2 _scroll;

#if ATK_TMP
        // TMP 宏方块下的向导默认字体（全局，存 ToolkitPrefabFonts）
        private TMP_FontAsset _defaultTmpFont;
        private bool _tmpFontFoldout;
#endif
#if ATK_TMP && ATK_LOCALIZATION
        // Localization 宏方块下的向导本地化字体（全局，存 ToolkitPrefabFonts；SerializeField 供原生属性绘制器绑定）
        [SerializeField] private LocalizedTmpFont localizedFont = new LocalizedTmpFont();
        private bool _localizedFontFoldout;
#endif

        [MenuItem("Tools/Ale Toolkit/Welcome", priority = 0)]
        public static void Open()
        {
            var win = GetWindow<ToolkitWelcomeWindow>(true, "Ale Toolkit", true);
            win.minSize = MinWindowSize;
            win.Show();
            win.Focus();
        }

        private void OnEnable() => _initialized = false;

        private void LoadState()
        {
            if (_initialized) return;
            _initialized   = true;
            _tmpEnabled    = ToolkitDefines.IsTmpEnabled();
            _tmpInstalled  = ToolkitDefines.IsTmpPackageInstalled();
            _locEnabled    = ToolkitDefines.IsLocalizationEnabled();
            _locInstalled  = ToolkitDefines.IsLocalizationPackageInstalled();
            _addrEnabled   = ToolkitDefines.IsAddressableEnabled();
            _addrInstalled = ToolkitDefines.IsAddressablePackageInstalled();
#if ATK_TMP
            _defaultTmpFont = ToolkitPrefabFonts.DefaultTmpFont;
#endif
#if ATK_TMP && ATK_LOCALIZATION
            localizedFont = ToolkitPrefabFonts.LocalizedFont;
#endif
        }

        private void OnGUI()
        {
            LoadState();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawSeparator();
            DrawLanguageSection();
            DrawSeparator();
            DrawToolsSection();
            DrawSeparator();
            DrawMacroSection();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Ale Toolkit", titleStyle);
            EditorGUILayout.LabelField(Tr("通用底层库 · 全局设置"), subStyle);
            EditorGUILayout.Space(4);
        }

        private void DrawLanguageSection()
        {
            EditorGUILayout.LabelField(Tr("界面语言"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawLangButton("中文",    EditorLanguage.ChineseSimplified);
            DrawLangButton("English", EditorLanguage.English);
            DrawLangButton("日本語",  EditorLanguage.Japanese);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            bool newEnum = EditorGUILayout.ToggleLeft(
                new GUIContent(Tr("枚举值"),
                    Tr("勾选后，类型下拉等枚举值也随语言切换；不勾选则保持代码中的英文原名。")),
                TranslateEnums);
            if (EditorGUI.EndChangeCheck()) TranslateEnums = newEnum;
        }

        private static void DrawLangButton(string label, EditorLanguage lang)
        {
            bool active = Current == lang;
            var prevBg = GUI.backgroundColor;
            if (active) GUI.backgroundColor = new Color(0.35f, 0.55f, 0.95f);
            if (GUILayout.Toggle(active, label, EditorStyles.miniButton,
                    GUILayout.Width(72), GUILayout.Height(22)) && !active)
                Current = lang;
            GUI.backgroundColor = prevBg;
        }

        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField(Tr("通用工具"), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
#if ATK_LOCALIZATION
            if (GUILayout.Button(Tr("打开 本地化工具窗口"), GUILayout.Height(26)))
                ToolkitLocalizationToolWindow.Open();
#endif
            // 仅在启用 ATK_ADDRESSABLE（迁移窗口已注入钩子）时显示。
            if (OpenAddressableMigration != null &&
                GUILayout.Button(Tr("打开 Addressable工具窗口"), GUILayout.Height(26)))
                OpenAddressableMigration();

            if (GUILayout.Button(Tr("查看文档"), GUILayout.Height(26)))
                OpenDocumentation();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                Tr("通用工具对任意数据文件（ScriptableObject）生效，遍历其全部属性值批量处理。"),
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawMacroSection()
        {
            EditorGUILayout.LabelField(Tr("插件支持（编译宏）"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Tr("项目级全局开关：一处启用即全项目生效，无需各插件分别设置。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            DrawMacroToggle("TextMeshPro", ToolkitDefines.Tmp, ToolkitDefines.PackageTmp,
                ref _tmpEnabled, _tmpInstalled,
                Tr("启用后，UI 脚本的文本组件使用 TMP_Text；未启用时使用 UnityEngine.UI.Text。Unity 2021+ 已内置 TextMeshPro，通常可直接启用。"),
                Tr("TMPro 命名空间未检测到。\n请确认 TextMeshPro 已通过 Package Manager 安装。\n\n确定要继续启用吗？"),
                DrawTmpFontField);

            EditorGUILayout.Space(2);

            DrawMacroToggle("Unity Localization", ToolkitDefines.Localization, ToolkitDefines.PackageLocalization,
                ref _locEnabled, _locInstalled,
                Tr("启用后，属性字段类型可选择 LocalizedString，支持 Unity Localization 多语言配置。"),
                Tr("com.unity.localization 包尚未安装。\n启用宏后，LocalizedString 字段将出现在编辑器中，但运行时无法解析。\n\n确定要继续启用吗？"),
                DrawLocalizedFontField);

            EditorGUILayout.Space(2);

            DrawMacroToggle("Unity Addressable", ToolkitDefines.Addressable, ToolkitDefines.PackageAddressables,
                ref _addrEnabled, _addrInstalled,
                Tr("启用后，属性系统的资源字段（Sprite/Prefab 等）在编辑器改用原生 AssetReference 选择器授权（仅存 GUID，" +
                   "不硬引用、加载数据库不再一并载入资源）；运行时经 Addressable 按需异步加载、引用计数随宿主销毁自动卸载。" +
                   "可用菜单 Tools/Ale Toolkit/Addressable/Addressable工具窗口在「Object 引用 ↔ AssetReference(GUID)」间批量互转。"),
                Tr("com.unity.addressables 包尚未安装。\n启用宏后，运行时无法通过 Addressable 加载资源。\n\n确定要继续启用吗？"));
        }

        /// <summary>
        /// 通用的插件宏开关绘制。Toggle 操作 PlayerSettings 宏（经 <see cref="DefineUtils.ApplyDefine"/>），
        /// 包未安装时勾选弹确认对话框。与库存版同构，但去除了向导字体等领域折叠栏。
        /// </summary>
        private void DrawMacroToggle(string titleName, string define, string package,
            ref bool enabled, bool packageInstalled, string description, string warnDialog,
            Action drawAdditionalFields = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.ToggleLeft(
                $"{titleName}  ({define})", enabled, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                if (newEnabled && !packageInstalled)
                {
                    if (!EditorUtility.DisplayDialog(Tr("警告"), warnDialog, Tr("确定"), Tr("取消")))
                        newEnabled = false;
                }
                if (newEnabled != enabled)
                {
                    enabled = newEnabled;
                    DefineUtils.ApplyDefine(enabled, define);
                    _pendingRecompile = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (packageInstalled)
            {
                var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
                EditorGUILayout.LabelField(Fmt("  ✓ {0} 已安装", package), s);
            }
            else
            {
                var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
                EditorGUILayout.LabelField(Fmt("  ⚠ {0} 未安装（需通过 Package Manager 安装）", package), s);
            }

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            if (_pendingRecompile)
            {
                var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
                EditorGUILayout.LabelField(Tr("  ⏳ 宏定义已更改，等待 Unity 重新编译…"), s);
                if (!EditorApplication.isCompiling) _pendingRecompile = false;
            }

            // 宏启用时绘制该宏方块下的额外全局配置（如向导字体）
            if (enabled && drawAdditionalFields != null)
            {
                EditorGUILayout.Space(4);
                drawAdditionalFields.Invoke();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>TextMeshPro 宏方块下的向导默认字体（全局，存 <see cref="ToolkitPrefabFonts"/>）。</summary>
        private void DrawTmpFontField()
        {
#if ATK_TMP
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _tmpFontFoldout = EditorGUILayout.Foldout(_tmpFontFoldout, Tr("TextMeshPro 设置"), true);
            if (_tmpFontFoldout)
            {
                EditorGUI.BeginChangeCheck();
                var newFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    Tr("默认字体"), _defaultTmpFont, typeof(TMP_FontAsset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _defaultTmpFont = newFont;
                    ToolkitPrefabFonts.DefaultTmpFont = newFont;
                }
                EditorGUILayout.LabelField(
                    Tr("生成测试 Prefab 时将此字体应用于所有 TMP 文本节点（留空则使用 TMP 默认字体）。"),
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
#endif
        }

        /// <summary>Unity Localization 宏方块下的向导本地化字体（全局，存 <see cref="ToolkitPrefabFonts"/>；需同时启用 ATK_TMP）。</summary>
        private void DrawLocalizedFontField()
        {
#if ATK_TMP && ATK_LOCALIZATION
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _localizedFontFoldout = EditorGUILayout.Foldout(_localizedFontFoldout, Tr("Unity Localization 设置"), true);
            if (_localizedFontFoldout)
            {
                var so   = new SerializedObject(this);
                var prop = so.FindProperty(nameof(localizedFont));
                if (prop != null)
                {
                    so.Update();
                    EditorGUILayout.PropertyField(prop, new GUIContent("Localized Asset Reference"));
                    so.ApplyModifiedProperties();

                    // 直接读取本地化引用的原始序列化字段（"GUID:..." / keyId / key）后按需持久化。
                    // 不依赖 ApplyModifiedProperties 的返回值（Localization 绘制器可能自行提交或经
                    // delayCall 延迟写入，返回值不可靠），也不经 JsonUtility（其不会触发嵌套结构体的
                    // ISerializationCallbackReceiver → ReferenceType 丢失 → 读回为空）。
                    var tableProp = prop.FindPropertyRelative("m_TableReference.m_TableCollectionName");
                    var keyIdProp = prop.FindPropertyRelative("m_TableEntryReference.m_KeyId");
                    var keyProp   = prop.FindPropertyRelative("m_TableEntryReference.m_Key");
                    if (tableProp != null && keyIdProp != null && keyProp != null)
                        ToolkitPrefabFonts.SaveLocalizedFont(
                            tableProp.stringValue, keyIdProp.longValue, keyProp.stringValue);
                }
                EditorGUILayout.LabelField(
                    Tr("生成测试 Prefab 时赋给 LocalizedFontEvent 组件的本地化字体资源。需同时启用 ATK_TMP 才生效。"),
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
#endif
        }

        private static void OpenDocumentation()
        {
            // README.md 不被 AssetDatabase 索引，直接取绝对路径后用系统默认程序打开。
            string abs = System.IO.Path.GetFullPath("Packages/com.ale.toolkit/README.md");
            if (System.IO.File.Exists(abs))
                Application.OpenURL("file:///" + abs.Replace('\\', '/'));
            else
                EditorUtility.DisplayDialog(Tr("文档未找到"),
                    Tr("未能找到文档文件：\nPackages/com.ale.toolkit/README.md"), Tr("确定"));
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
        }
    }
}
