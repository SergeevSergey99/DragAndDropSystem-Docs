#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DragAndDropSystem.Inspector;
using UnityEditor;
using UnityEngine;

namespace DragAndDropSystem.Inspector.Editor
{
    internal static class InspectorReflectionUtility
    {
        public static T GetAttribute<T>(SerializedProperty property) where T : Attribute
        {
            FieldInfo field = GetFieldInfo(property);
            if (field == null)
                return null;

            return Attribute.GetCustomAttribute(field, typeof(T), true) as T;
        }

        public static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            if (property == null)
                return null;

            Type currentType = property.serializedObject.targetObject.GetType();
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');
            FieldInfo currentField = null;

            for (int i = 0; i < elements.Length; i++)
            {
                string element = elements[i];
                int bracketIndex = element.IndexOf('[');
                string memberName = bracketIndex >= 0 ? element.Substring(0, bracketIndex) : element;

                currentField = GetFieldInfo(currentType, memberName);
                if (currentField == null)
                    return null;

                currentType = ResolveFieldType(currentType, currentField);
                if (bracketIndex >= 0)
                {
                    currentType = GetElementType(currentType);
                }
            }

            return currentField;
        }

        public static Type GetPropertyValueType(SerializedProperty property)
        {
            if (property == null)
                return null;

            Type currentType = property.serializedObject.targetObject.GetType();
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length; i++)
            {
                string element = elements[i];
                int bracketIndex = element.IndexOf('[');
                string memberName = bracketIndex >= 0 ? element.Substring(0, bracketIndex) : element;

                FieldInfo currentField = GetFieldInfo(currentType, memberName);
                if (currentField == null)
                    return null;

                currentType = ResolveFieldType(currentType, currentField);
                if (bracketIndex >= 0)
                {
                    currentType = GetElementType(currentType);
                }
            }

            return currentType;
        }

        public static object GetParentObject(SerializedProperty property)
        {
            if (property == null)
                return null;

            object current = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                current = GetPathValue(current, elements[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        public static object GetMemberValue(object source, string memberName)
        {
            if (source == null || string.IsNullOrEmpty(memberName))
                return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type currentType = source.GetType();

            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(source);

                PropertyInfo property = currentType.GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(source);

                MethodInfo method = currentType.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
                if (method != null)
                    return method.Invoke(source, null);

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static object GetPathValue(object source, string pathElement)
        {
            if (source == null)
                return null;

            int bracketIndex = pathElement.IndexOf('[');
            if (bracketIndex < 0)
                return GetMemberValue(source, pathElement);

            string memberName = pathElement.Substring(0, bracketIndex);
            object enumerableObject = GetMemberValue(source, memberName);
            IList list = enumerableObject as IList;
            if (list == null)
                return null;

            int endBracketIndex = pathElement.IndexOf(']', bracketIndex + 1);
            if (endBracketIndex < 0)
                return null;

            string indexText = pathElement.Substring(bracketIndex + 1, endBracketIndex - bracketIndex - 1);
            if (!int.TryParse(indexText, out int index) || index < 0 || index >= list.Count)
                return null;

            return list[index];
        }

        public static IEnumerable<MemberInfo> GetShowInInspectorMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var members = new List<MemberInfo>();

            Type currentType = type;
            while (currentType != null && currentType != typeof(UnityEngine.Object))
            {
                FieldInfo[] fields = currentType.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (Attribute.IsDefined(fields[i], typeof(ShowInInspectorAttribute), true))
                        members.Add(fields[i]);
                }

                PropertyInfo[] properties = currentType.GetProperties(flags);
                for (int i = 0; i < properties.Length; i++)
                {
                    if (Attribute.IsDefined(properties[i], typeof(ShowInInspectorAttribute), true))
                        members.Add(properties[i]);
                }

                currentType = currentType.BaseType;
            }

            members.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return members;
        }

        public static IEnumerable<MethodInfo> GetButtonMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var methods = new List<MethodInfo>();

            Type currentType = type;
            while (currentType != null && currentType != typeof(UnityEngine.Object))
            {
                MethodInfo[] declaredMethods = currentType.GetMethods(flags);
                for (int i = 0; i < declaredMethods.Length; i++)
                {
                    MethodInfo method = declaredMethods[i];
                    if (method.IsSpecialName || method.GetParameters().Length != 0)
                        continue;

                    if (Attribute.IsDefined(method, typeof(ButtonAttribute), true))
                        methods.Add(method);
                }

                currentType = currentType.BaseType;
            }

            methods.Sort((a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            return methods;
        }

        public static object GetMemberValue(object source, MemberInfo member)
        {
            var field = member as FieldInfo;
            if (field != null)
                return field.GetValue(source);

            var property = member as PropertyInfo;
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(source, null);

            return null;
        }

        private static Type GetElementType(Type collectionType)
        {
            if (collectionType == null)
                return null;

            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (collectionType.IsGenericType)
                return collectionType.GetGenericArguments()[0];

            return typeof(object);
        }

        private static Type ResolveFieldType(Type ownerType, FieldInfo field)
        {
            if (field == null)
                return null;

            Type fieldType = field.FieldType;
            if (!fieldType.ContainsGenericParameters)
                return fieldType;

            Dictionary<Type, Type> genericArguments = BuildGenericArgumentMap(ownerType, field.DeclaringType);
            return ReplaceGenericParameters(fieldType, genericArguments);
        }

        private static Dictionary<Type, Type> BuildGenericArgumentMap(Type ownerType, Type declaringType)
        {
            var map = new Dictionary<Type, Type>();
            if (ownerType == null || declaringType == null)
                return map;

            Type declaringDefinition = declaringType.IsGenericType
                ? declaringType.GetGenericTypeDefinition()
                : declaringType;

            Type currentType = ownerType;
            while (currentType != null)
            {
                Type currentDefinition = currentType.IsGenericType
                    ? currentType.GetGenericTypeDefinition()
                    : currentType;

                if (currentDefinition == declaringDefinition)
                {
                    if (currentDefinition.IsGenericTypeDefinition)
                    {
                        Type[] declaredArguments = currentDefinition.GetGenericArguments();
                        Type[] actualArguments = currentType.GetGenericArguments();
                        for (int i = 0; i < declaredArguments.Length && i < actualArguments.Length; i++)
                        {
                            map[declaredArguments[i]] = actualArguments[i];
                        }
                    }

                    break;
                }

                currentType = currentType.BaseType;
            }

            return map;
        }

        private static Type ReplaceGenericParameters(Type type, Dictionary<Type, Type> genericArguments)
        {
            if (type == null)
                return null;

            if (type.IsGenericParameter)
            {
                Type resolvedType;
                return genericArguments.TryGetValue(type, out resolvedType) ? resolvedType : type;
            }

            if (type.IsArray)
            {
                Type elementType = ReplaceGenericParameters(type.GetElementType(), genericArguments);
                return elementType == null ? type : elementType.MakeArrayType();
            }

            if (!type.IsGenericType)
                return type;

            Type[] arguments = type.GetGenericArguments();
            Type[] resolvedArguments = new Type[arguments.Length];
            bool changed = false;

            for (int i = 0; i < arguments.Length; i++)
            {
                resolvedArguments[i] = ReplaceGenericParameters(arguments[i], genericArguments);
                changed |= resolvedArguments[i] != arguments[i];
            }

            if (!changed)
                return type;

            return type.GetGenericTypeDefinition().MakeGenericType(resolvedArguments);
        }

        private static FieldInfo GetFieldInfo(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                    return field;

                type = type.BaseType;
            }

            return null;
        }
    }

    internal static class FoldoutGroupStateCache
    {
        private static readonly Dictionary<string, bool> StateByKey = new Dictionary<string, bool>();

        public static bool Get(UnityEngine.Object target, FoldoutGroupAttribute attribute)
        {
            string key = BuildKey(target, attribute.GroupName);
            bool state;
            if (StateByKey.TryGetValue(key, out state))
                return state;

            StateByKey[key] = attribute.expanded;
            return attribute.expanded;
        }

        public static void Set(UnityEngine.Object target, string groupName, bool value)
        {
            StateByKey[BuildKey(target, groupName)] = value;
        }

        private static string BuildKey(UnityEngine.Object target, string groupName)
        {
            int instanceId = target != null ? target.GetInstanceID() : 0;
            string typeName = target != null ? target.GetType().FullName : "null";
            return typeName + ":" + instanceId + ":" + groupName;
        }
    }

    internal static class InspectorPreviewUtility
    {
        public static Rect GetAspectFitRect(Rect bounds, float sourceWidth, float sourceHeight)
        {
            if (sourceWidth <= 0f || sourceHeight <= 0f || bounds.width <= 0f || bounds.height <= 0f)
                return bounds;

            float sourceAspect = sourceWidth / sourceHeight;
            float boundsAspect = bounds.width / bounds.height;

            if (sourceAspect > boundsAspect)
            {
                float fittedHeight = bounds.width / sourceAspect;
                return new Rect(bounds.x, bounds.y + (bounds.height - fittedHeight) * 0.5f, bounds.width, fittedHeight);
            }

            float fittedWidth = bounds.height * sourceAspect;
            return new Rect(bounds.x + (bounds.width - fittedWidth) * 0.5f, bounds.y, fittedWidth, bounds.height);
        }
    }

    internal static class FoldoutGroupStyles
    {
        private static GUIStyle _boxStyle;
        private static GUIStyle _headerFoldoutStyle;
        private static readonly Dictionary<Color, GUIStyle> ContentStyleCache = new Dictionary<Color, GUIStyle>();

        public static GUIStyle BoxStyle
        {
            get
            {
                if (_boxStyle == null)
                {
                    _boxStyle = new GUIStyle(EditorStyles.helpBox);
                    _boxStyle.padding = new RectOffset(1, 1, 1, 1);
                    _boxStyle.margin = new RectOffset(0, 0, 4, 4);
                }
                return _boxStyle;
            }
        }

        public static GUIStyle HeaderFoldoutStyle
        {
            get
            {
                if (_headerFoldoutStyle == null)
                {
                    _headerFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                    _headerFoldoutStyle.fontStyle = FontStyle.Bold;
                }
                return _headerFoldoutStyle;
            }
        }

        public static Color DefaultHeaderColor => EditorGUIUtility.isProSkin
            ? new Color(0.28f, 0.28f, 0.28f)
            : new Color(0.72f, 0.72f, 0.72f);

        public static Color DefaultContentColor => EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 0.35f)
            : new Color(0.84f, 0.84f, 0.84f, 0.35f);

        public static Color SeparatorColor => EditorGUIUtility.isProSkin
            ? new Color(0.1f, 0.1f, 0.1f, 0.6f)
            : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;
        }

        public static GUIStyle GetContentStyle(Color color)
        {
            if (ContentStyleCache.TryGetValue(color, out var style) && style?.normal?.background != null)
                return style;

            style = new GUIStyle();
            style.padding = new RectOffset(6, 4, 0, 0);
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.DontSave;
            style.normal.background = tex;
            ContentStyleCache[color] = style;
            return style;
        }
    }

    internal abstract class GroupedInspectorEditorBase : UnityEditor.Editor
    {
        private const string ScriptPropertyName = "m_Script";
        private readonly HashSet<string> _renderedGroupNames = new HashSet<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _renderedGroupNames.Clear();

            DrawScriptReference();
            DrawSerializedProperties();
            DrawShowInInspectorMembers();
            DrawButtonMethods();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScriptReference()
        {
            SerializedProperty script = serializedObject.FindProperty(ScriptPropertyName);
            if (script == null)
                return;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(script);
            }
        }

        private void DrawSerializedProperties()
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            var orderedEntries = new List<object>();
            var groupedProperties = new Dictionary<string, List<SerializedProperty>>();
            var groupAttributes = new Dictionary<string, FoldoutGroupAttribute>();

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == ScriptPropertyName)
                    continue;

                SerializedProperty property = iterator.Copy();

                if (!ShouldShowProperty(property))
                    continue;

                FoldoutGroupAttribute group = InspectorReflectionUtility.GetAttribute<FoldoutGroupAttribute>(property);
                string groupName = group?.GroupName;

                if (group == null)
                {
                    orderedEntries.Add(property);
                    continue;
                }

                if (!groupedProperties.TryGetValue(groupName, out List<SerializedProperty> properties))
                {
                    properties = new List<SerializedProperty>();
                    groupedProperties[groupName] = properties;
                    groupAttributes[groupName] = group;
                    orderedEntries.Add(groupName);
                }

                properties.Add(property);
            }

            for (int i = 0; i < orderedEntries.Count; i++)
            {
                string groupedName = orderedEntries[i] as string;
                if (groupedName != null)
                {
                    FoldoutGroupAttribute group = groupAttributes[groupedName];
                    List<SerializedProperty> properties = groupedProperties[groupedName];
                    DrawGroupBox(group, () =>
                    {
                        for (int j = 0; j < properties.Count; j++)
                            EditorGUILayout.PropertyField(properties[j], true);

                        DrawButtonsForGroup(groupedName);
                    });
                    _renderedGroupNames.Add(groupedName);
                    continue;
                }

                EditorGUILayout.PropertyField((SerializedProperty)orderedEntries[i], true);
            }
        }

        private bool ShouldShowProperty(SerializedProperty property)
        {
            ShowIfAttribute showIf = InspectorReflectionUtility.GetAttribute<ShowIfAttribute>(property);
            if (showIf == null)
                return true;

            object parent = InspectorReflectionUtility.GetParentObject(property);
            object conditionValue = InspectorReflectionUtility.GetMemberValue(parent, showIf.ConditionMemberName);

            if (conditionValue == null)
                return false;

            if (string.IsNullOrEmpty(showIf.ExpectedValue))
                return conditionValue is bool boolValue && boolValue;

            return string.Equals(conditionValue.ToString(), showIf.ExpectedValue, StringComparison.Ordinal);
        }

        private void DrawShowInInspectorMembers()
        {
            IEnumerable<MemberInfo> members = InspectorReflectionUtility.GetShowInInspectorMembers(target.GetType());
            string currentGroupName = null;
            FoldoutGroupAttribute currentGroupAttr = null;
            List<MemberInfo> currentGroupMembers = null;

            foreach (MemberInfo member in members)
            {
                if (HasSerializedBacking(member))
                    continue;

                FoldoutGroupAttribute group = Attribute.GetCustomAttribute(member, typeof(FoldoutGroupAttribute), true) as FoldoutGroupAttribute;
                string groupName = group?.GroupName;

                if (groupName != currentGroupName)
                {
                    FlushMemberGroup(currentGroupAttr, currentGroupMembers);

                    if (group != null)
                    {
                        currentGroupName = groupName;
                        currentGroupAttr = group;
                        currentGroupMembers = new List<MemberInfo> { member };
                    }
                    else
                    {
                        currentGroupName = null;
                        currentGroupAttr = null;
                        currentGroupMembers = null;
                        DrawShowInInspectorMember(member);
                    }
                    continue;
                }

                if (group != null)
                {
                    currentGroupMembers.Add(member);
                }
                else
                {
                    DrawShowInInspectorMember(member);
                }
            }

            FlushMemberGroup(currentGroupAttr, currentGroupMembers);
        }

        private void FlushMemberGroup(FoldoutGroupAttribute group, List<MemberInfo> members)
        {
            if (group == null || members == null || members.Count == 0)
                return;

            DrawGroupBox(group, () =>
            {
                foreach (var member in members)
                    DrawShowInInspectorMember(member);

                DrawButtonsForGroup(group.GroupName);
            });
            _renderedGroupNames.Add(group.GroupName);
        }

        private void DrawGroupBox(FoldoutGroupAttribute group, Action drawContent)
        {
            Color headerColor = FoldoutGroupStyles.ParseColor(group.HeaderColor, FoldoutGroupStyles.DefaultHeaderColor);
            Color contentColor = FoldoutGroupStyles.ParseColor(group.ContentColor, FoldoutGroupStyles.DefaultContentColor);

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginVertical(FoldoutGroupStyles.BoxStyle);

            // Header bar
            Rect headerRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(headerRect, headerColor);

            // Foldout
            Rect foldoutRect = new Rect(headerRect.x + 14f, headerRect.y + 1f, headerRect.width - 18f, headerRect.height - 2f);
            bool expanded = FoldoutGroupStateCache.Get(target, group);
            expanded = EditorGUI.Foldout(foldoutRect, expanded, group.GroupName, true, FoldoutGroupStyles.HeaderFoldoutStyle);
            FoldoutGroupStateCache.Set(target, group.GroupName, expanded);

            if (expanded)
            {
                // Separator line
                Rect separatorRect = new Rect(headerRect.x, headerRect.yMax, headerRect.width, 1f);
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(separatorRect, FoldoutGroupStyles.SeparatorColor);

                // Content with background
                GUIStyle contentStyle = FoldoutGroupStyles.GetContentStyle(contentColor);
                EditorGUILayout.BeginVertical(contentStyle);
                EditorGUILayout.Space(4f);
                using (new EditorGUI.IndentLevelScope())
                {
                    drawContent();
                }
                EditorGUILayout.Space(4f);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private bool HasSerializedBacking(MemberInfo member)
        {
            var field = member as FieldInfo;
            return field != null && serializedObject.FindProperty(field.Name) != null;
        }

        private void DrawShowInInspectorMember(MemberInfo member)
        {
            object value = InspectorReflectionUtility.GetMemberValue(target, member);
            string label = ObjectNames.NicifyVariableName(member.Name);
            LabelTextAttribute labelText = Attribute.GetCustomAttribute(member, typeof(LabelTextAttribute), true) as LabelTextAttribute;
            if (labelText != null && !string.IsNullOrEmpty(labelText.Text))
                label = labelText.Text;

            using (new EditorGUI.DisabledScope(true))
            {
                DrawReadOnlyValue(label, value);
            }
        }

        private static void DrawReadOnlyValue(string label, object value)
        {
            if (value == null)
            {
                EditorGUILayout.LabelField(label, "Null");
                return;
            }

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (unityObject != null)
            {
                EditorGUILayout.ObjectField(label, unityObject, unityObject.GetType(), true);
                return;
            }

            Type valueType = value.GetType();
            if (valueType.IsEnum)
            {
                EditorGUILayout.EnumPopup(label, (Enum)value);
                return;
            }

            if (value is bool)
            {
                EditorGUILayout.Toggle(label, (bool)value);
                return;
            }

            if (value is int)
            {
                EditorGUILayout.IntField(label, (int)value);
                return;
            }

            if (value is float)
            {
                EditorGUILayout.FloatField(label, (float)value);
                return;
            }

            if (value is string)
            {
                EditorGUILayout.TextField(label, (string)value);
                return;
            }

            EditorGUILayout.LabelField(label, value.ToString());
        }

        private void DrawButtonMethods()
        {
            IEnumerable<MethodInfo> methods = InspectorReflectionUtility.GetButtonMethods(target.GetType());
            string currentGroupName = null;
            FoldoutGroupAttribute currentGroupAttr = null;
            List<MethodInfo> currentGroupMethods = null;

            foreach (MethodInfo method in methods)
            {
                FoldoutGroupAttribute group = Attribute.GetCustomAttribute(method, typeof(FoldoutGroupAttribute), true) as FoldoutGroupAttribute;
                string groupName = group?.GroupName;

                if (groupName != null && _renderedGroupNames.Contains(groupName))
                    continue;

                if (groupName != currentGroupName)
                {
                    FlushButtonGroup(currentGroupAttr, currentGroupMethods);

                    if (group != null)
                    {
                        currentGroupName = groupName;
                        currentGroupAttr = group;
                        currentGroupMethods = new List<MethodInfo> { method };
                    }
                    else
                    {
                        currentGroupName = null;
                        currentGroupAttr = null;
                        currentGroupMethods = null;
                        DrawButtonMethod(method);
                    }

                    continue;
                }

                if (group != null)
                {
                    currentGroupMethods.Add(method);
                }
                else
                {
                    DrawButtonMethod(method);
                }
            }

            FlushButtonGroup(currentGroupAttr, currentGroupMethods);
        }

        private void FlushButtonGroup(FoldoutGroupAttribute group, List<MethodInfo> methods)
        {
            if (group == null || methods == null || methods.Count == 0)
                return;

            DrawGroupBox(group, () =>
            {
                for (int i = 0; i < methods.Count; i++)
                    DrawButtonMethod(methods[i]);
            });
            _renderedGroupNames.Add(group.GroupName);
        }

        private void DrawButtonMethod(MethodInfo method)
        {
            ButtonAttribute button = Attribute.GetCustomAttribute(method, typeof(ButtonAttribute), true) as ButtonAttribute;
            if (button == null)
                return;

            string label = string.IsNullOrEmpty(button.Label)
                ? ObjectNames.NicifyVariableName(method.Name)
                : button.Label;

            bool disableInEditMode = Attribute.IsDefined(method, typeof(DisableInEditorModeAttribute), true);
            bool shouldDisable = disableInEditMode && !Application.isPlaying;

            using (new EditorGUI.DisabledScope(shouldDisable))
            {
                if (!GUILayout.Button(label, GUILayout.Height(22f)))
                    return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                UnityEngine.Object currentTarget = targets[i];
                Undo.RecordObject(currentTarget, label);
                method.Invoke(currentTarget, null);
                EditorUtility.SetDirty(currentTarget);
            }
        }

        private void DrawButtonsForGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
                return;

            List<MethodInfo> methods = GetGroupedButtonMethods(groupName);
            for (int i = 0; i < methods.Count; i++)
            {
                DrawButtonMethod(methods[i]);
            }
        }

        private List<MethodInfo> GetGroupedButtonMethods(string groupName)
        {
            var result = new List<MethodInfo>();
            foreach (MethodInfo method in InspectorReflectionUtility.GetButtonMethods(target.GetType()))
            {
                FoldoutGroupAttribute group = Attribute.GetCustomAttribute(method, typeof(FoldoutGroupAttribute), true) as FoldoutGroupAttribute;
                if (group != null && group.GroupName == groupName)
                    result.Add(method);
            }

            return result;
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    internal sealed class GroupedMonoBehaviourEditor : GroupedInspectorEditorBase
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true, isFallback = true)]
    internal sealed class GroupedScriptableObjectEditor : GroupedInspectorEditorBase
    {
    }

    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public sealed class ShowIfPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!ShouldShow(property))
                return;

            EditorGUI.PropertyField(position, property, label, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldShow(property)
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : -EditorGUIUtility.standardVerticalSpacing;
        }

        private bool ShouldShow(SerializedProperty property)
        {
            var showIf = (ShowIfAttribute)attribute;
            object parent = InspectorReflectionUtility.GetParentObject(property);
            object conditionValue = InspectorReflectionUtility.GetMemberValue(parent, showIf.ConditionMemberName);

            if (conditionValue == null)
                return false;

            if (string.IsNullOrEmpty(showIf.ExpectedValue))
            {
                return conditionValue is bool boolValue && boolValue;
            }

            return string.Equals(conditionValue.ToString(), showIf.ExpectedValue, StringComparison.Ordinal);
        }
    }

    [CustomPropertyDrawer(typeof(RequiredAttribute))]
    public sealed class RequiredPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);
            Rect fieldRect = new Rect(position.x, position.y, position.width, propertyHeight);
            EditorGUI.PropertyField(fieldRect, property, label, true);

            if (!NeedsWarning(property))
                return;

            Rect helpRect = new Rect(
                position.x,
                fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight * 2f);
            EditorGUI.HelpBox(helpRect, $"{label.text} is required.", MessageType.Warning);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label, true);
            if (NeedsWarning(property))
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUIUtility.singleLineHeight * 2f;
            }

            return height;
        }

        private static bool NeedsWarning(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.ObjectReference
                   && property.objectReferenceValue == null;
        }
    }

    [CustomPropertyDrawer(typeof(EnumToggleButtonsAttribute))]
    public sealed class EnumToggleButtonsPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            Rect buttonsRect = new Rect(
                position.x + EditorGUIUtility.labelWidth + 2f,
                position.y,
                position.width - EditorGUIUtility.labelWidth - 2f,
                position.height);

            EditorGUI.LabelField(labelRect, label);

            string[] names = property.enumDisplayNames;
            int selected = property.enumValueIndex;
            int newSelected = GUI.Toolbar(buttonsRect, selected, names);
            if (newSelected != selected)
                property.enumValueIndex = newSelected;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public sealed class ReadOnlyPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    [CustomPropertyDrawer(typeof(HideLabelAttribute))]
    public sealed class HideLabelPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!property.hasVisibleChildren)
            {
                EditorGUI.PropertyField(position, property, GUIContent.none, false);
                return;
            }

            // Complex property — draw children directly without foldout or label indent
            float y = position.y;
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true))
                return;

            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                    break;

                float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                Rect childRect = new Rect(position.x, y, position.width, childHeight);
                EditorGUI.PropertyField(childRect, iterator, true);
                y = childRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
            while (iterator.NextVisible(false));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.hasVisibleChildren)
                return EditorGUIUtility.singleLineHeight;

            float height = 0f;
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true))
                return EditorGUIUtility.singleLineHeight;

            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                    break;

                if (height > 0f)
                    height += EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(iterator, true);
            }
            while (iterator.NextVisible(false));

            return Mathf.Max(height, EditorGUIUtility.singleLineHeight);
        }
    }

    [CustomPropertyDrawer(typeof(LabelTextAttribute))]
    public sealed class LabelTextPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelText = (LabelTextAttribute)attribute;
            EditorGUI.PropertyField(position, property, new GUIContent(labelText.Text), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    [CustomPropertyDrawer(typeof(InfoBoxAttribute))]
    public sealed class InfoBoxDecoratorDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            var infoBox = (InfoBoxAttribute)attribute;
            EditorGUI.HelpBox(position, infoBox.Message, ConvertMessageType(infoBox.MessageType));
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        private static MessageType ConvertMessageType(InfoMessageType messageType)
        {
            switch (messageType)
            {
                case InfoMessageType.Warning:
                    return MessageType.Warning;
                case InfoMessageType.Error:
                    return MessageType.Error;
                default:
                    return MessageType.Info;
            }
        }
    }

    [CustomPropertyDrawer(typeof(TitleAttribute))]
    public sealed class TitleDecoratorDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            var title = (TitleAttribute)attribute;
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            switch (title.TitleAlignment)
            {
                case TitleAlignments.Centered:
                    style.alignment = TextAnchor.MiddleCenter;
                    break;
                case TitleAlignments.Right:
                    style.alignment = TextAnchor.MiddleRight;
                    break;
                default:
                    style.alignment = TextAnchor.MiddleLeft;
                    break;
            }

            EditorGUI.LabelField(position, title.Title, style);
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(TitleGroupAttribute))]
    public sealed class TitleGroupDecoratorDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            var title = (TitleGroupAttribute)attribute;
            EditorGUI.LabelField(position, title.Title, EditorStyles.boldLabel);
        }

        public override float GetHeight()
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(PreviewFieldAttribute))]
    public sealed class PreviewFieldPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var previewField = (PreviewFieldAttribute)attribute;
            float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);
            Rect fieldRect = new Rect(position.x, position.y, position.width, propertyHeight);
            EditorGUI.PropertyField(fieldRect, property, label, true);

            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                return;

            Texture texture = GetPreviewTexture(property.objectReferenceValue);
            if (texture == null)
                return;

            Rect previewRect = new Rect(
                position.x,
                fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                previewField.Height);

            GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);
            DrawPreview(previewRect, property.objectReferenceValue, texture);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label, true);
            if (property.propertyType == SerializedPropertyType.ObjectReference
                && property.objectReferenceValue != null
                && GetPreviewTexture(property.objectReferenceValue) != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += ((PreviewFieldAttribute)attribute).Height;
            }

            return height;
        }

        private static Texture GetPreviewTexture(UnityEngine.Object target)
        {
            Sprite sprite = target as Sprite;
            if (sprite != null)
            {
                return sprite.texture;
            }

            Texture texture = target as Texture;
            if (texture != null)
            {
                return texture;
            }

            return AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target);
        }

        private static void DrawPreview(Rect previewRect, UnityEngine.Object target, Texture texture)
        {
            Rect contentRect = new Rect(previewRect.x + 4f, previewRect.y + 4f, previewRect.width - 8f, previewRect.height - 8f);

            if (target is Sprite sprite)
            {
                Rect fittedRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, sprite.rect.width, sprite.rect.height);
                Rect uv = new Rect(
                    sprite.rect.x / sprite.texture.width,
                    sprite.rect.y / sprite.texture.height,
                    sprite.rect.width / sprite.texture.width,
                    sprite.rect.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(fittedRect, texture, uv, true);
                return;
            }

            Rect textureRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, texture.width, texture.height);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, true);
        }
    }

    [CustomPropertyDrawer(typeof(DragAndDropSystem.Examples.Minecraft.CraftingRecipePattern))]
    public sealed class CraftingRecipePatternPropertyDrawer : PropertyDrawer
    {
        private const float CellSize = 72f;
        private const float CellFooterHeight = 18f;
        private const float CellPadding = 4f;
        private const int GridSize = 3;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty cellsProperty = property.FindPropertyRelative("_cells");
            EnsureArraySize(cellsProperty, DragAndDropSystem.Examples.Minecraft.CraftingRecipePattern.CellCount);

            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            float y = labelRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            float totalCellHeight = CellSize + CellFooterHeight;

            for (int row = 0; row < GridSize; row++)
            {
                for (int col = 0; col < GridSize; col++)
                {
                    int index = row * GridSize + col;
                    SerializedProperty element = cellsProperty.GetArrayElementAtIndex(index);

                    float x = position.x + col * (CellSize + EditorGUIUtility.standardVerticalSpacing);
                    Rect cellRect = new Rect(x, y, CellSize, totalCellHeight);
                    GUI.Box(cellRect, GUIContent.none, EditorStyles.helpBox);

                    Rect previewRect = new Rect(
                        cellRect.x + CellPadding,
                        cellRect.y + CellPadding,
                        cellRect.width - CellPadding * 2f,
                        CellSize - CellPadding * 2f);
                    DrawCellPreview(previewRect, element.objectReferenceValue);

                    Rect fieldRect = new Rect(
                        cellRect.x + 1f,
                        cellRect.yMax - CellFooterHeight,
                        cellRect.width - 2f,
                        CellFooterHeight);

                    EditorGUI.BeginProperty(cellRect, GUIContent.none, element);
                    UnityEngine.Object newValue = EditorGUI.ObjectField(
                        fieldRect,
                        GUIContent.none,
                        element.objectReferenceValue,
                        typeof(DragAndDropSystem.Examples.Minecraft.MinecraftItemSO),
                        false);
                    if (newValue != element.objectReferenceValue)
                    {
                        element.objectReferenceValue = newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    EditorGUI.EndProperty();
                }

                y += totalCellHeight + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float totalCellHeight = CellSize + CellFooterHeight;
            return EditorGUIUtility.singleLineHeight
                   + EditorGUIUtility.standardVerticalSpacing
                   + GridSize * totalCellHeight
                   + (GridSize - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        private static void EnsureArraySize(SerializedProperty property, int expectedSize)
        {
            if (property == null || !property.isArray || property.propertyType == SerializedPropertyType.String)
                return;

            if (property.arraySize == expectedSize)
                return;

            property.arraySize = expectedSize;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DrawCellPreview(Rect previewRect, UnityEngine.Object target)
        {
            GUI.Box(previewRect, GUIContent.none, EditorStyles.objectFieldThumb);
            if (target == null)
                return;

            Texture texture = GetPreviewTexture(target);
            if (texture == null)
                return;

            Rect contentRect = new Rect(
                previewRect.x + 2f,
                previewRect.y + 2f,
                previewRect.width - 4f,
                previewRect.height - 4f);

            if (texture is Texture2D texture2D && target is DragAndDropSystem.Examples.Minecraft.MinecraftItemSO item && item.Icon != null)
            {
                Sprite sprite = item.Icon;
                Rect fittedRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, sprite.rect.width, sprite.rect.height);
                Rect uv = new Rect(
                    sprite.rect.x / texture2D.width,
                    sprite.rect.y / texture2D.height,
                    sprite.rect.width / texture2D.width,
                    sprite.rect.height / texture2D.height);
                GUI.DrawTextureWithTexCoords(fittedRect, texture, uv, true);
                return;
            }

            Rect textureRect = InspectorPreviewUtility.GetAspectFitRect(contentRect, texture.width, texture.height);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, true);
        }

        private static Texture GetPreviewTexture(UnityEngine.Object target)
        {
            if (target is DragAndDropSystem.Examples.Minecraft.MinecraftItemSO item && item.Icon != null)
                return item.Icon.texture;

            if (target is Sprite sprite)
                return sprite.texture;

            if (target is Texture texture)
                return texture;

            return target != null ? AssetPreview.GetAssetPreview(target) ?? AssetPreview.GetMiniThumbnail(target) : null;
        }

    }

    [CustomPropertyDrawer(typeof(ManagedReferencePickerAttribute))]
    public sealed class ManagedReferencePickerPropertyDrawer : PropertyDrawer
    {
        private const float SmallButtonWidth = 24f;
        private const float PickButtonWidth = 20f;
        private const float BoxPadding = 4f;

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return false;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsObjectReferenceList(property))
            {
                DrawObjectReferenceList(position, property, label);
                return;
            }

            if (IsManagedReferenceList(property))
            {
                DrawList(position, property, label);
                return;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                DrawObjectReferenceSingle(position, property, label, GetReferenceFieldType(property), false, null);
                return;
            }

            DrawSingle(position, property, label, GetManagedReferenceFieldType(property), false, null);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsObjectReferenceList(property))
                return GetObjectReferenceListHeight(property);

            if (IsManagedReferenceList(property))
                return GetListHeight(property, label);

            if (property.propertyType == SerializedPropertyType.ObjectReference)
                return EditorGUIUtility.singleLineHeight;

            return GetSingleHeight(property, label);
        }

        private void DrawList(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 14f, headerRect.height);
            Rect labelRect = new Rect(foldoutRect.xMax, headerRect.y, headerRect.width - 70f, headerRect.height);
            Rect addButtonRect = new Rect(headerRect.xMax - 64f, headerRect.y, 64f, headerRect.height);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            EditorGUI.LabelField(labelRect, $"{label.text} ({property.arraySize})");

            if (GUI.Button(addButtonRect, "Add"))
            {
                ShowAddMenu(property, GetListElementType(property));
            }

            if (!property.isExpanded)
                return;

            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            Type elementType = GetListElementType(property);

            if (property.arraySize == 0)
            {
                Rect emptyRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(emptyRect, "No entries configured.", MessageType.Info);
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float elementHeight = GetSingleHeight(element, new GUIContent($"Element {i}"));
                float boxHeight = elementHeight + BoxPadding * 2f;
                Rect boxRect = new Rect(position.x, y, position.width, boxHeight);
                GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                Rect contentRect = new Rect(
                    boxRect.x + BoxPadding,
                    boxRect.y + BoxPadding,
                    boxRect.width - BoxPadding * 2f,
                    elementHeight);

                DrawSingle(contentRect, element, new GUIContent($"Element {i}"), elementType, true, () =>
                {
                    DrawListControls(contentRect, property, i);
                });

                y = boxRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private void DrawObjectReferenceList(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 14f, headerRect.height);
            Rect labelRect = new Rect(foldoutRect.xMax, headerRect.y, headerRect.width - 70f, headerRect.height);
            Rect addButtonRect = new Rect(headerRect.xMax - 64f, headerRect.y, 64f, headerRect.height);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            EditorGUI.LabelField(labelRect, $"{label.text} ({property.arraySize})");

            using (new EditorGUI.DisabledScope(GetListElementType(property) == null))
            {
                if (GUI.Button(addButtonRect, "Add"))
                {
                    int index = property.arraySize;
                    property.arraySize++;
                    SerializedProperty element = property.GetArrayElementAtIndex(index);
                    element.objectReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            if (!property.isExpanded)
                return;

            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            Type elementType = GetListElementType(property);

            if (property.arraySize == 0)
            {
                Rect emptyRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(emptyRect, "No entries configured.", MessageType.Info);
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float boxHeight = EditorGUIUtility.singleLineHeight + BoxPadding * 2f;
                Rect boxRect = new Rect(position.x, y, position.width, boxHeight);
                GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                Rect contentRect = new Rect(
                    boxRect.x + BoxPadding,
                    boxRect.y + BoxPadding,
                    boxRect.width - BoxPadding * 2f,
                    EditorGUIUtility.singleLineHeight);

                DrawObjectReferenceSingle(contentRect, element, new GUIContent($"Element {i}"), elementType, true, () =>
                {
                    DrawListControls(contentRect, property, i);
                });

                y = boxRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private float GetListHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            if (property.arraySize == 0)
                return height + EditorGUIUtility.singleLineHeight * 2f;

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                height += GetSingleHeight(element, new GUIContent($"Element {i}")) + BoxPadding * 2f;
                height += EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private float GetObjectReferenceListHeight(SerializedProperty property)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            if (property.arraySize == 0)
                return height + EditorGUIUtility.singleLineHeight * 2f;

            for (int i = 0; i < property.arraySize; i++)
            {
                height += EditorGUIUtility.singleLineHeight + BoxPadding * 2f;
                height += EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private void DrawSingle(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Type managedReferenceType,
            bool drawExtraControls,
            Action extraControlsDrawer)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            float controlsWidth = drawExtraControls ? SmallButtonWidth * 3f + EditorGUIUtility.standardVerticalSpacing * 2f : 0f;
            bool hasChildren = property.managedReferenceValue != null && EnumerateChildren(property).Any();

            // Foldout arrow only when there are visible children
            if (hasChildren)
            {
                Rect foldoutRect = new Rect(headerRect.x + 14, headerRect.y, 14f, headerRect.height);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            }

            // Label — indent only when foldout is present
            float foldoutIndent = hasChildren ? 14f : 0f;
            Rect labelRect = new Rect(
                headerRect.x + foldoutIndent,
                headerRect.y,
                EditorGUIUtility.labelWidth - foldoutIndent,
                headerRect.height);
            EditorGUI.LabelField(labelRect, label);

            // Pick button (small, right-aligned before optional list controls)
            Rect pickButtonRect = new Rect(
                headerRect.xMax - controlsWidth - PickButtonWidth,
                headerRect.y,
                PickButtonWidth,
                headerRect.height);

            // Type name field (fills space between label and pick button)
            float typeFieldX = position.x + EditorGUIUtility.labelWidth-12;
            Rect typeNameRect = new Rect(
                typeFieldX,
                headerRect.y,
                pickButtonRect.x - typeFieldX - 2f,
                headerRect.height);

            string typeName = GetTypeButtonLabel(property, managedReferenceType);
            EditorGUI.LabelField(typeNameRect, typeName, EditorStyles.textField);

            if (GUI.Button(pickButtonRect, "\u25BC"))
            {
                ShowTypeMenu(property, managedReferenceType, allowNull: true);
            }

            if (drawExtraControls)
            {
                extraControlsDrawer?.Invoke();
            }

            if (!hasChildren || !property.isExpanded)
                return;

            float indent = 15f;
            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            foreach (var child in EnumerateChildren(property))
            {
                float childHeight = EditorGUI.GetPropertyHeight(child, true);
                Rect childRect = new Rect(position.x + indent, y, position.width - indent, childHeight);
                EditorGUI.PropertyField(childRect, child, true);
                y = childRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private void DrawObjectReferenceSingle(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Type objectType,
            bool drawExtraControls,
            Action extraControlsDrawer)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            float controlsWidth = drawExtraControls ? SmallButtonWidth * 3f + EditorGUIUtility.standardVerticalSpacing * 2f : 0f;
            Rect fieldRect = new Rect(headerRect.x, headerRect.y, headerRect.width - controlsWidth, headerRect.height);

            EditorGUI.BeginProperty(position, label, property);
            UnityEngine.Object newValue = EditorGUI.ObjectField(
                fieldRect,
                label,
                property.objectReferenceValue,
                objectType ?? typeof(UnityEngine.Object),
                false);

            if (newValue != property.objectReferenceValue)
            {
                property.objectReferenceValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            if (drawExtraControls)
            {
                extraControlsDrawer?.Invoke();
            }

            EditorGUI.EndProperty();
        }

        private float GetSingleHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            bool hasChildren = property.managedReferenceValue != null && EnumerateChildren(property).Any();
            if (!hasChildren || !property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            foreach (var child in EnumerateChildren(property))
            {
                height += EditorGUI.GetPropertyHeight(child, true);
                height += EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private void DrawListControls(Rect contentRect, SerializedProperty listProperty, int index)
        {
            float controlsRight = contentRect.xMax;
            Rect deleteRect = new Rect(controlsRight - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);
            Rect downRect = new Rect(deleteRect.x - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);
            Rect upRect = new Rect(downRect.x - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUI.Button(upRect, "\u2191"))
                {
                    listProperty.MoveArrayElement(index, index - 1);
                    listProperty.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= listProperty.arraySize - 1))
            {
                if (GUI.Button(downRect, "\u2193"))
                {
                    listProperty.MoveArrayElement(index, index + 1);
                    listProperty.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(deleteRect, "X"))
            {
                listProperty.DeleteArrayElementAtIndex(index);
                listProperty.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void ShowAddMenu(SerializedProperty listProperty, Type elementType)
        {
            var menu = new GenericMenu();
            foreach (Type candidateType in GetAssignableTypes(elementType))
            {
                menu.AddItem(new GUIContent(candidateType.Name), false, () =>
                {
                    int index = listProperty.arraySize;
                    listProperty.arraySize++;
                    SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
                    element.managedReferenceValue = Activator.CreateInstance(candidateType);
                    listProperty.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private void ShowTypeMenu(SerializedProperty property, Type baseType, bool allowNull)
        {
            var menu = new GenericMenu();
            if (allowNull)
            {
                menu.AddItem(new GUIContent("None"), property.managedReferenceValue == null, () =>
                {
                    property.managedReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator(string.Empty);
            }

            foreach (Type candidateType in GetAssignableTypes(baseType))
            {
                bool isCurrentType = property.managedReferenceValue != null
                                     && property.managedReferenceValue.GetType() == candidateType;

                menu.AddItem(new GUIContent(candidateType.Name), isCurrentType, () =>
                {
                    property.managedReferenceValue = Activator.CreateInstance(candidateType);
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        private static IEnumerable<SerializedProperty> EnumerateChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            if (!iterator.NextVisible(true))
                yield break;

            do
            {
                if (SerializedProperty.EqualContents(iterator, end))
                    yield break;

                yield return iterator.Copy();
            }
            while (iterator.NextVisible(false));
        }

        private string GetTypeButtonLabel(SerializedProperty property, Type baseType)
        {
            if (property.managedReferenceValue != null)
                return ObjectNames.NicifyVariableName(property.managedReferenceValue.GetType().Name);

            string baseName = GetDisplayTypeName(baseType);
            return $"None ({baseName})";
        }

        private static string GetDisplayTypeName(Type type)
        {
            if (type == null)
                return "?";

            if (!type.IsGenericType)
                return ObjectNames.NicifyVariableName(type.Name);

            string typeName = type.Name;
            int tickIndex = typeName.IndexOf('`');
            if (tickIndex >= 0)
            {
                typeName = typeName.Substring(0, tickIndex);
            }

            string[] argumentNames = type.GetGenericArguments()
                .Select(GetDisplayTypeName)
                .ToArray();

            return $"{ObjectNames.NicifyVariableName(typeName)}<{string.Join(", ", argumentNames)}>";
        }

        private static IEnumerable<Type> GetAssignableTypes(Type baseType)
        {
            if (baseType == null)
                yield break;

            if (!baseType.IsAbstract && !baseType.IsInterface && IsSerializableManagedReferenceType(baseType))
            {
                yield return baseType;
            }

            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (IsSerializableManagedReferenceType(type))
                    yield return type;
            }
        }

        private static bool IsSerializableManagedReferenceType(Type type)
        {
            return type != null
                   && !type.IsAbstract
                   && !type.IsGenericTypeDefinition
                   && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                   && type.GetConstructor(Type.EmptyTypes) != null
                   && (type.IsDefined(typeof(SerializableAttribute), false) || type.IsClass);
        }

        private bool IsManagedReferenceList(SerializedProperty property)
        {
            return property.isArray
                   && property.propertyType != SerializedPropertyType.String
                   && !IsObjectReferenceList(property)
                   && GetListElementType(property) != null;
        }

        private bool IsObjectReferenceList(SerializedProperty property)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                return false;

            Type elementType = GetListElementType(property);
            return elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType);
        }

        private Type GetManagedReferenceFieldType(SerializedProperty property)
        {
            return InspectorReflectionUtility.GetPropertyValueType(property) ?? fieldInfo.FieldType;
        }

        private Type GetReferenceFieldType(SerializedProperty property)
        {
            return InspectorReflectionUtility.GetPropertyValueType(property) ?? fieldInfo.FieldType;
        }

        private Type GetListElementType(SerializedProperty property)
        {
            Type fieldType = InspectorReflectionUtility.GetPropertyValueType(property) ?? fieldInfo.FieldType;
            if (fieldType.IsArray)
                return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length == 1)
                return fieldType.GetGenericArguments()[0];

            return null;
        }
    }

    [CustomPropertyDrawer(typeof(RulePresetPickerAttribute))]
    public sealed class RulePresetPickerPropertyDrawer : PropertyDrawer
    {
        private const float SmallButtonWidth = 24f;
        private const float BoxPadding = 4f;

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return false;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IsObjectReferenceList(property))
            {
                DrawList(position, property, label);
                return;
            }

            DrawSingle(position, property, label, GetReferenceFieldType(property), false, null);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (IsObjectReferenceList(property))
                return GetListHeight(property);

            return EditorGUIUtility.singleLineHeight;
        }

        private void DrawList(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 14f, headerRect.height);
            Rect labelRect = new Rect(foldoutRect.xMax, headerRect.y, headerRect.width - 70f, headerRect.height);
            Rect addButtonRect = new Rect(headerRect.xMax - 64f, headerRect.y, 64f, headerRect.height);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            EditorGUI.LabelField(labelRect, $"{label.text} ({property.arraySize})");

            if (GUI.Button(addButtonRect, "Add"))
            {
                int index = property.arraySize;
                property.arraySize++;
                SerializedProperty element = property.GetArrayElementAtIndex(index);
                element.objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            }

            if (!property.isExpanded)
                return;

            float y = headerRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            Type elementType = GetListElementType(property);

            if (property.arraySize == 0)
            {
                Rect emptyRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(emptyRect, "No entries configured.", MessageType.Info);
                return;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                float boxHeight = EditorGUIUtility.singleLineHeight + BoxPadding * 2f;
                Rect boxRect = new Rect(position.x, y, position.width, boxHeight);
                GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);

                Rect contentRect = new Rect(
                    boxRect.x + BoxPadding,
                    boxRect.y + BoxPadding,
                    boxRect.width - BoxPadding * 2f,
                    EditorGUIUtility.singleLineHeight);

                DrawSingle(contentRect, element, new GUIContent($"Element {i}"), elementType, true, () =>
                {
                    DrawListControls(contentRect, property, i);
                });

                y = boxRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        private float GetListHeight(SerializedProperty property)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return height;

            height += EditorGUIUtility.standardVerticalSpacing;
            if (property.arraySize == 0)
                return height + EditorGUIUtility.singleLineHeight * 2f;

            for (int i = 0; i < property.arraySize; i++)
            {
                height += EditorGUIUtility.singleLineHeight + BoxPadding * 2f;
                height += EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        private void DrawSingle(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Type referenceType,
            bool drawExtraControls,
            Action extraControlsDrawer)
        {
            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            float controlsWidth = drawExtraControls ? SmallButtonWidth * 3f + EditorGUIUtility.standardVerticalSpacing * 2f : 0f;
            Rect fieldRect = new Rect(headerRect.x, headerRect.y, headerRect.width - controlsWidth, headerRect.height);

            EditorGUI.BeginProperty(position, label, property);
            Type pickerType = GetPickerFieldType(referenceType);
            UnityEngine.Object newValue = EditorGUI.ObjectField(
                fieldRect,
                label,
                property.objectReferenceValue,
                pickerType,
                false);

            if (newValue != property.objectReferenceValue)
            {
                property.objectReferenceValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            if (drawExtraControls)
            {
                extraControlsDrawer?.Invoke();
            }

            EditorGUI.EndProperty();
        }

        private void DrawListControls(Rect contentRect, SerializedProperty listProperty, int index)
        {
            float controlsRight = contentRect.xMax;
            Rect deleteRect = new Rect(controlsRight - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);
            Rect downRect = new Rect(deleteRect.x - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);
            Rect upRect = new Rect(downRect.x - SmallButtonWidth, contentRect.y, SmallButtonWidth, EditorGUIUtility.singleLineHeight);

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUI.Button(upRect, "\u2191"))
                {
                    listProperty.MoveArrayElement(index, index - 1);
                    listProperty.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUI.DisabledScope(index >= listProperty.arraySize - 1))
            {
                if (GUI.Button(downRect, "\u2193"))
                {
                    listProperty.MoveArrayElement(index, index + 1);
                    listProperty.serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(deleteRect, "X"))
            {
                listProperty.DeleteArrayElementAtIndex(index);
                listProperty.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private static Type GetPickerFieldType(Type referenceType)
        {
            if (referenceType == null)
                return typeof(UnityEngine.Object);

            if (!referenceType.IsGenericType && typeof(UnityEngine.Object).IsAssignableFrom(referenceType))
                return referenceType;

            List<Type> candidates = TypeCache.GetTypesDerivedFrom(referenceType)
                .Where(type => !type.IsAbstract && typeof(UnityEngine.Object).IsAssignableFrom(type))
                .ToList();

            if (candidates.Count == 0)
                return typeof(UnityEngine.Object).IsAssignableFrom(referenceType) ? referenceType : typeof(UnityEngine.Object);

            Type sharedBase = GetMostSpecificSharedPickerType(candidates);
            if (sharedBase != null)
                return sharedBase;

            return candidates[0];
        }

        private static Type GetMostSpecificSharedPickerType(List<Type> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            Type current = candidates[0];
            while (current != null && current != typeof(UnityEngine.Object))
            {
                if (!current.IsGenericType
                    && current != typeof(ScriptableObject)
                    && typeof(UnityEngine.Object).IsAssignableFrom(current)
                    && candidates.All(candidate => current.IsAssignableFrom(candidate)))
                {
                    return current;
                }

                current = current.BaseType;
            }

            return null;
        }

        private bool IsObjectReferenceList(SerializedProperty property)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
                return false;

            Type elementType = GetListElementType(property);
            return elementType != null && typeof(UnityEngine.Object).IsAssignableFrom(elementType);
        }

        private Type GetReferenceFieldType(SerializedProperty property)
        {
            return InspectorReflectionUtility.GetPropertyValueType(property) ?? fieldInfo.FieldType;
        }

        private Type GetListElementType(SerializedProperty property)
        {
            Type fieldType = InspectorReflectionUtility.GetPropertyValueType(property) ?? fieldInfo.FieldType;
            if (fieldType == null)
                return null;

            if (fieldType.IsArray)
                return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length == 1)
                return fieldType.GetGenericArguments()[0];

            return null;
        }
    }
}
#endif
