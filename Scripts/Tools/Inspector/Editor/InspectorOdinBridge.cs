#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;

namespace UniversalDragAndDrop.Tools.Inspector.Editor
{
    internal sealed class DragAndDropOdinAttributeProcessor : OdinAttributeProcessor
    {
        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            return member != null
                   && (Attribute.IsDefined(member, typeof(FoldoutGroupAttribute), true)
                       || Attribute.IsDefined(member, typeof(ShowIfAttribute), true)
                       || Attribute.IsDefined(member, typeof(InfoBoxAttribute), true)
                       || Attribute.IsDefined(member, typeof(RequiredAttribute), true)
                       || Attribute.IsDefined(member, typeof(ReadOnlyAttribute), true)
                       || Attribute.IsDefined(member, typeof(HideLabelAttribute), true)
                       || Attribute.IsDefined(member, typeof(EnumToggleButtonsAttribute), true)
                       || Attribute.IsDefined(member, typeof(LabelTextAttribute), true)
                       || Attribute.IsDefined(member, typeof(TitleAttribute), true)
                       || Attribute.IsDefined(member, typeof(TitleGroupAttribute), true)
                       || Attribute.IsDefined(member, typeof(PreviewFieldAttribute), true)
                       || Attribute.IsDefined(member, typeof(OnValueChangedAttribute), true)
                       || Attribute.IsDefined(member, typeof(ButtonAttribute), true)
                       || Attribute.IsDefined(member, typeof(ShowInInspectorAttribute), true)
                       || Attribute.IsDefined(member, typeof(DisableInEditorModeAttribute), true));
        }

        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes)
        {
            AddFoldoutGroup(attributes);
            AddShowIf(attributes);
            AddInfoBox(attributes);
            AddRequired(attributes);
            AddReadOnly(attributes);
            AddHideLabel(attributes);
            AddEnumToggleButtons(attributes);
            AddLabelText(attributes);
            AddTitle(attributes);
            AddTitleGroup(attributes);
            AddPreviewField(attributes);
            AddOnValueChanged(attributes);
            AddButton(attributes);
            AddShowInInspector(attributes);
            AddDisableInEditorMode(attributes);
        }

        private static void AddFoldoutGroup(List<Attribute> attributes)
        {
            FoldoutGroupAttribute attribute = FindAttribute<FoldoutGroupAttribute>(attributes);
            if (attribute == null)
                return;

            attributes.Add(new Sirenix.OdinInspector.FoldoutGroupAttribute(attribute.GroupName, attribute.expanded));
        }

        private static void AddShowIf(List<Attribute> attributes)
        {
            ShowIfAttribute attribute = FindAttribute<ShowIfAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.ConditionMemberName))
                return;

            if (string.IsNullOrEmpty(attribute.ExpectedValue))
            {
                attributes.Add(new Sirenix.OdinInspector.ShowIfAttribute(attribute.ConditionMemberName));
                return;
            }

            attributes.Add(new Sirenix.OdinInspector.ShowIfAttribute(attribute.ConditionMemberName, attribute.ExpectedValue));
        }

        private static void AddInfoBox(List<Attribute> attributes)
        {
            InfoBoxAttribute attribute = FindAttribute<InfoBoxAttribute>(attributes);
            if (attribute == null)
                return;

            attributes.Add(new Sirenix.OdinInspector.InfoBoxAttribute(attribute.Message, ConvertInfoMessageType(attribute.MessageType)));
        }

        private static void AddRequired(List<Attribute> attributes)
        {
            if (FindAttribute<RequiredAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.RequiredAttribute());
        }

        private static void AddReadOnly(List<Attribute> attributes)
        {
            if (FindAttribute<ReadOnlyAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.ReadOnlyAttribute());
        }

        private static void AddHideLabel(List<Attribute> attributes)
        {
            if (FindAttribute<HideLabelAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.HideLabelAttribute());
        }

        private static void AddEnumToggleButtons(List<Attribute> attributes)
        {
            if (FindAttribute<EnumToggleButtonsAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.EnumToggleButtonsAttribute());
        }

        private static void AddLabelText(List<Attribute> attributes)
        {
            LabelTextAttribute attribute = FindAttribute<LabelTextAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.Text))
                return;

            attributes.Add(new Sirenix.OdinInspector.LabelTextAttribute(attribute.Text));
        }

        private static void AddTitle(List<Attribute> attributes)
        {
            TitleAttribute attribute = FindAttribute<TitleAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.Title))
                return;

            attributes.Add(new Sirenix.OdinInspector.TitleAttribute(attribute.Title, null, ConvertTitleAlignment(attribute.TitleAlignment)));
        }

        private static void AddTitleGroup(List<Attribute> attributes)
        {
            TitleGroupAttribute attribute = FindAttribute<TitleGroupAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.Title))
                return;

            attributes.Add(new Sirenix.OdinInspector.TitleGroupAttribute(attribute.Title));
        }

        private static void AddPreviewField(List<Attribute> attributes)
        {
            PreviewFieldAttribute attribute = FindAttribute<PreviewFieldAttribute>(attributes);
            if (attribute == null)
                return;

            attributes.Add(new Sirenix.OdinInspector.PreviewFieldAttribute(attribute.Height));
        }

        private static void AddOnValueChanged(List<Attribute> attributes)
        {
            OnValueChangedAttribute attribute = FindAttribute<OnValueChangedAttribute>(attributes);
            if (attribute == null || string.IsNullOrEmpty(attribute.MethodName))
                return;

            attributes.Add(new Sirenix.OdinInspector.OnValueChangedAttribute(attribute.MethodName));
        }

        private static void AddButton(List<Attribute> attributes)
        {
            ButtonAttribute attribute = FindAttribute<ButtonAttribute>(attributes);
            if (attribute == null)
                return;

            if (string.IsNullOrEmpty(attribute.Label))
            {
                attributes.Add(new Sirenix.OdinInspector.ButtonAttribute());
                return;
            }

            attributes.Add(new Sirenix.OdinInspector.ButtonAttribute(attribute.Label));
        }

        private static void AddShowInInspector(List<Attribute> attributes)
        {
            if (FindAttribute<ShowInInspectorAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.ShowInInspectorAttribute());
        }

        private static void AddDisableInEditorMode(List<Attribute> attributes)
        {
            if (FindAttribute<DisableInEditorModeAttribute>(attributes) != null)
                attributes.Add(new Sirenix.OdinInspector.DisableInEditorModeAttribute());
        }

        private static T FindAttribute<T>(List<Attribute> attributes) where T : Attribute
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is T match)
                    return match;
            }

            return null;
        }

        private static Sirenix.OdinInspector.InfoMessageType ConvertInfoMessageType(InfoMessageType messageType)
        {
            switch (messageType)
            {
                case InfoMessageType.Warning:
                    return Sirenix.OdinInspector.InfoMessageType.Warning;
                case InfoMessageType.Error:
                    return Sirenix.OdinInspector.InfoMessageType.Error;
                default:
                    return Sirenix.OdinInspector.InfoMessageType.Info;
            }
        }

        private static Sirenix.OdinInspector.TitleAlignments ConvertTitleAlignment(TitleAlignments alignment)
        {
            switch (alignment)
            {
                case TitleAlignments.Centered:
                    return Sirenix.OdinInspector.TitleAlignments.Centered;
                case TitleAlignments.Right:
                    return Sirenix.OdinInspector.TitleAlignments.Right;
                default:
                    return Sirenix.OdinInspector.TitleAlignments.Left;
            }
        }
    }
}
#endif
