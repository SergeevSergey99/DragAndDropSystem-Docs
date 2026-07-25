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

        /// <summary>
        /// When true, the activation callbacks touch native Unity state (transform) the way
        /// a real presenter does. Counter-only callbacks are pure managed code and stay
        /// silent on a destroyed object, which makes them useless for proving that the
        /// manager survives a destroyed target — only a native access raises
        /// MissingReferenceException.
        /// </summary>
        public bool TouchNativeStateOnActivation { get; set; }

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

        public void OnBecomeActiveTarget()
        {
            ActivateCount++;
            TouchNativeState();
        }

        public void OnBecomeInactiveTarget()
        {
            DeactivateCount++;
            TouchNativeState();
        }

        /// <summary>
        /// Stands in for the highlight/geometry work a real drop target does. Reading
        /// transform on a destroyed component throws MissingReferenceException, which is
        /// exactly what a uGUI presenter or a detached VisualElement adapter would hit.
        /// </summary>
        private void TouchNativeState()
        {
            if (!TouchNativeStateOnActivation)
                return;

            LastTouchedPosition = transform.position;
        }

        /// <summary>Kept so the native read cannot be optimized away.</summary>
        public Vector3 LastTouchedPosition { get; private set; }

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
