using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Tests
{
    /// <summary>
    /// MonoBehaviour-backed IDropTarget. Unlike <see cref="FakeDropTarget"/>, this one can
    /// actually be destroyed while it sits on the manager's drop-target stack, which is the
    /// only way to reproduce a view that vanishes without popping itself.
    ///
    /// uGUI hides this case behind DropAreaBase.OnDisable -> PopDropTarget. A VisualElement
    /// has no OnDisable, so a UI Toolkit target detached from its panel will hit exactly
    /// this path unless its adapter handles DetachFromPanelEvent explicitly.
    /// </summary>
    /// <remarks>
    /// [ExecuteAlways] is required: EditMode does not run OnEnable/OnDisable for plain
    /// MonoBehaviours. DropAreaBase gets those callbacks for free only because Selectable
    /// carries the attribute — a UI Toolkit drop target will not, so any EditMode coverage
    /// of its detach handling needs the same opt-in.
    /// </remarks>
    [ExecuteAlways]
    public sealed class FakeDropTargetBehaviour : MonoBehaviour, IDropTarget
    {
        private BaseSlot _targetSlot;
        private IDropProcessor _processor;

        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }

        /// <summary>Set to false to skip the OnDisable-based pop, simulating a detached view.</summary>
        public bool PopSelfOnDisable { get; set; }

        /// <summary>How many times OnDisable ran. Distinguishes "hook never fired" from
        /// "hook fired but the pop did not take effect".</summary>
        public int DisableCalls { get; private set; }

        /// <summary>True once OnDisable actually reached PopDropTarget.</summary>
        public bool PopWasAttempted { get; private set; }

        public static FakeDropTargetBehaviour Create(
            string name = "FakeDropTargetBehaviour",
            BaseSlot targetSlot = null,
            IDropProcessor processor = null)
        {
            var go = new GameObject(name);
            var target = go.AddComponent<FakeDropTargetBehaviour>();
            target._targetSlot = targetSlot;
            target._processor = processor;
            return target;
        }

        public BaseSlot GetTargetSlot() => _targetSlot;

        public IDropProcessor GetDropProcessor() => _processor;

        public void OnBecomeActiveTarget() => ActivateCount++;

        public void OnBecomeInactiveTarget() => DeactivateCount++;

        private void OnDisable()
        {
            DisableCalls++;

            if (!PopSelfOnDisable || !DragAndDropManager.IsInstanceExist)
                return;

            var manager = DragAndDropManager.Instance;
            if (manager == null || !manager.IsDragging)
                return;

            PopWasAttempted = true;
            manager.PopDropTarget(this);
        }
    }
}
