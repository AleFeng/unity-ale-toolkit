using UnityEditor;
using UnityEngine;

namespace Ale.Effect.Editor
{
    /// <summary>
    /// <see cref="EffectMagnitude"/> 的 PropertyDrawer：一行「来源类型」下拉 + 依类型切换的字段——
    /// Scalable：基数 / 每级；SetByCaller：键 / 回退；AttributeBased：属性（经宿主钩子）/ 取自，第二行 系数 / 前加 / 后加。
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectMagnitude))]
    public sealed class EffectMagnitudeDrawer : PropertyDrawer
    {
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;

        private const float KindW  = 96f;
        private const float MiniLabelW = 34f;

        /// <summary>行数：AttributeBased 两行，其余一行。</summary>
        public static int Rows(SerializedProperty property)
            => (EMagnitudeKind)property.FindPropertyRelative("kind").enumValueIndex == EMagnitudeKind.AttributeBased ? 2 : 1;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Rows(property) * RowH - V;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            var kindProp = property.FindPropertyRelative("kind");
            var kind = (EMagnitudeKind)kindProp.enumValueIndex;

            var row1 = new Rect(position.x, position.y, position.width, LH);
            var rest = label != GUIContent.none && !string.IsNullOrEmpty(label.text) ? EditorGUI.PrefixLabel(row1, label) : row1;

            var kindRect = new Rect(rest.x, rest.y, KindW, LH);
            EditorGUI.BeginChangeCheck();
            int newKind = EditorGUI.Popup(kindRect, kindProp.enumValueIndex, kindProp.enumDisplayNames);
            if (EditorGUI.EndChangeCheck()) kindProp.enumValueIndex = newKind;

            float fx = kindRect.xMax + 4f;
            float fw = Mathf.Max(40f, rest.xMax - fx);
            switch (kind)
            {
                case EMagnitudeKind.SetByCaller:
                    DrawPair(fx, rest.y, fw,
                        "键", r => DrawText(r, property.FindPropertyRelative("setByCallerKey")),
                        "回退", r => EditorGUI.PropertyField(r, property.FindPropertyRelative("baseValue"), GUIContent.none));
                    break;
                case EMagnitudeKind.AttributeBased:
                {
                    var attr = property.FindPropertyRelative("attributeId");
                    DrawPair(fx, rest.y, fw,
                        "属性", r =>
                        {
                            EditorGUI.BeginChangeCheck();
                            string v = EffectDefinitionDrawerHooks.DrawAttributeId(r, attr.stringValue);
                            if (EditorGUI.EndChangeCheck()) attr.stringValue = v;
                        },
                        "取自", r => EditorGUI.PropertyField(r, property.FindPropertyRelative("captureFrom"), GUIContent.none));

                    var row2 = new Rect(rest.x, rest.y + RowH, rest.width, LH);
                    DrawTriple(row2.x, row2.y, row2.width,
                        "系数", property.FindPropertyRelative("coefficient"),
                        "前加", property.FindPropertyRelative("preAdd"),
                        "后加", property.FindPropertyRelative("postAdd"));
                    break;
                }
                default:
                    DrawPair(fx, rest.y, fw,
                        "基数", r => EditorGUI.PropertyField(r, property.FindPropertyRelative("baseValue"), GUIContent.none),
                        "每级", r => EditorGUI.PropertyField(r, property.FindPropertyRelative("perLevel"), GUIContent.none));
                    break;
            }
            EditorGUI.EndProperty();
        }

        private static void DrawText(Rect r, SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            string v = EditorGUI.TextField(r, prop.stringValue ?? string.Empty);
            if (EditorGUI.EndChangeCheck()) prop.stringValue = v;
        }

        private static void DrawPair(float x, float y, float width, string l1, System.Action<Rect> f1, string l2, System.Action<Rect> f2)
        {
            float half = (width - 4f) * 0.5f;
            DrawLabeled(new Rect(x, y, half, LH), l1, f1);
            DrawLabeled(new Rect(x + half + 4f, y, half, LH), l2, f2);
        }

        private static void DrawTriple(float x, float y, float width, string l1, SerializedProperty p1, string l2, SerializedProperty p2, string l3, SerializedProperty p3)
        {
            float third = (width - 8f) / 3f;
            DrawLabeled(new Rect(x, y, third, LH), l1, r => EditorGUI.PropertyField(r, p1, GUIContent.none));
            DrawLabeled(new Rect(x + third + 4f, y, third, LH), l2, r => EditorGUI.PropertyField(r, p2, GUIContent.none));
            DrawLabeled(new Rect(x + (third + 4f) * 2f, y, third, LH), l3, r => EditorGUI.PropertyField(r, p3, GUIContent.none));
        }

        private static void DrawLabeled(Rect r, string miniLabel, System.Action<Rect> field)
        {
            EditorGUI.LabelField(new Rect(r.x, r.y, MiniLabelW, LH), miniLabel, EditorStyles.miniLabel);
            field(new Rect(r.x + MiniLabelW, r.y, Mathf.Max(20f, r.width - MiniLabelW), LH));
        }
    }

    /// <summary>
    /// <see cref="EffectModifier"/> 的 PropertyDrawer：第一行「属性 id（经宿主钩子）+ 运算」，其后嵌套幅度绘制器。
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectModifier))]
    public sealed class EffectModifierDrawer : PropertyDrawer
    {
        private static float LH   => EditorGUIUtility.singleLineHeight;
        private static float V    => EditorGUIUtility.standardVerticalSpacing;
        private static float RowH => LH + V;
        private const float OpW = 96f;
        private static readonly GUIContent MagnitudeLabel = new GUIContent("幅度");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => RowH + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("magnitude"), MagnitudeLabel, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            var attr = property.FindPropertyRelative("attributeId");
            var op   = property.FindPropertyRelative("operation");

            var row1 = new Rect(position.x, position.y, position.width, LH);
            var rest = label != GUIContent.none && !string.IsNullOrEmpty(label.text) ? EditorGUI.PrefixLabel(row1, label) : row1;
            var attrRect = new Rect(rest.x, rest.y, Mathf.Max(40f, rest.width - OpW - 4f), LH);
            var opRect   = new Rect(rest.xMax - OpW, rest.y, OpW, LH);

            EditorGUI.BeginChangeCheck();
            string v = EffectDefinitionDrawerHooks.DrawAttributeId(attrRect, attr.stringValue);
            if (EditorGUI.EndChangeCheck()) attr.stringValue = v;
            EditorGUI.PropertyField(opRect, op, GUIContent.none);

            var mag = property.FindPropertyRelative("magnitude");
            float magH = EditorGUI.GetPropertyHeight(mag, MagnitudeLabel, true);
            EditorGUI.PropertyField(new Rect(position.x, position.y + RowH, position.width, magH), mag, MagnitudeLabel, true);
            EditorGUI.EndProperty();
        }
    }
}
