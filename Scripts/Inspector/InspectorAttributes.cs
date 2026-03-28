using System;
using UnityEngine;

namespace DragAndDropSystem.Inspector
{
    public enum InfoMessageType
    {
        None,
        Info,
        Warning,
        Error
    }

    public enum TitleAlignments
    {
        Left,
        Centered,
        Right
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
    public sealed class FoldoutGroupAttribute : PropertyAttribute
    {
        public string GroupName { get; }
        public bool expanded;
        public string HeaderColor { get; set; }
        public string ContentColor { get; set; }

        public FoldoutGroupAttribute(string groupName, bool expanded = true)
        {
            GroupName = groupName;
            this.expanded = expanded;
        }

        public FoldoutGroupAttribute(string groupName, string headerColor, bool expanded = true)
        {
            GroupName = groupName;
            this.expanded = expanded;
            HeaderColor = headerColor;
        }

        public FoldoutGroupAttribute(string groupName, string headerColor, string contentColor, bool expanded = true)
        {
            GroupName = groupName;
            this.expanded = expanded;
            HeaderColor = headerColor;
            ContentColor = contentColor;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class InfoBoxAttribute : PropertyAttribute
    {
        public string Message { get; }
        public InfoMessageType MessageType { get; }

        public InfoBoxAttribute(string message)
            : this(message, InfoMessageType.Info)
        {
        }

        public InfoBoxAttribute(string message, InfoMessageType messageType)
        {
            Message = message;
            MessageType = messageType;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class RequiredAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionMemberName { get; }
        public string ExpectedValue { get; }

        public ShowIfAttribute(string conditionMemberName)
        {
            ConditionMemberName = conditionMemberName;
        }

        public ShowIfAttribute(string conditionMemberName, string expectedValue)
        {
            ConditionMemberName = conditionMemberName;
            ExpectedValue = expectedValue;
        }

        /// <summary>
        /// Поддержка enum и других значимых типов: ShowIf(nameof(field), MyEnum.Value)
        /// </summary>
        public ShowIfAttribute(string conditionMemberName, object expectedValue)
        {
            ConditionMemberName = conditionMemberName;
            ExpectedValue = expectedValue?.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class HideLabelAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Label { get; }

        public ButtonAttribute()
        {
        }

        public ButtonAttribute(string label)
        {
            Label = label;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class DisableInEditorModeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class EnumToggleButtonsAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class LabelTextAttribute : PropertyAttribute
    {
        public string Text { get; }

        public LabelTextAttribute(string text)
        {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ReadOnlyAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class ShowInInspectorAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ListDrawerSettingsAttribute : PropertyAttribute
    {
        public bool DraggableItems { get; set; }
        public bool ShowPaging { get; set; }
        public bool ShowFoldout { get; set; }
        public bool ShowIndexLabels { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class TitleAttribute : PropertyAttribute
    {
        public string Title { get; }
        public TitleAlignments TitleAlignment { get; set; } = TitleAlignments.Left;

        public TitleAttribute(string title)
        {
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class InlinePropertyAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class PreviewFieldAttribute : PropertyAttribute
    {
        public float Height { get; }

        public PreviewFieldAttribute(float height = 64f)
        {
            Height = height;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class TitleGroupAttribute : PropertyAttribute
    {
        public string Title { get; }

        public TitleGroupAttribute(string title)
        {
            Title = title;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class OnValueChangedAttribute : PropertyAttribute
    {
        public string MethodName { get; }

        public OnValueChangedAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class ManagedReferencePickerAttribute : PropertyAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class RulePresetPickerAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// Запрещает добавлять и удалять элементы массива или списка в инспекторе.
    /// Размер массива фиксируется — редактировать можно только сами элементы.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class FixedArraySizeAttribute : PropertyAttribute
    {
    }
}
