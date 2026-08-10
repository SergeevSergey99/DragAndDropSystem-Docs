using System.Collections.Generic;
using System.Text;
using UDND.Interaction;
using UDND.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UDND.Tools
{
    /// <summary>
    /// Scene setup checks for the pieces a drag needs but that fail silently when missing.
    ///
    /// DragAndDropManager owns drag state only: rendering lives in DragVisualPresenter, input in
    /// InputEventRouter. Both auto-create themselves on first use, so a hand-assembled scene runs
    /// without a single error while rendering nothing — which is exactly the trap reported here.
    ///
    /// Editor and development builds only: these are authoring mistakes, not runtime conditions.
    /// </summary>
    public static class UDNDSetupValidator
    {
        private const string DragCanvasPrefabPath =
            "UniversalDragAndDrop/Prefabs/DragCanvas.prefab";

        /// <summary>
        /// Logs one warning listing everything missing or misconfigured. Silent when the scene is fine.
        /// </summary>
        public static void LogSceneSetupIssues()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var issues = new List<string>();
            CollectDragVisualIssues(issues);
            CollectInputIssues(issues);

            if (issues.Count == 0)
                return;

            var message = new StringBuilder("[UDND] Scene setup is incomplete:");
            for (int i = 0; i < issues.Count; i++)
                message.Append("\n  - ").Append(issues[i]);

            message
                .Append("\n\nDragAndDropManager, DragVisualPresenter and InputEventRouter all live on ")
                .Append(DragCanvasPrefabPath)
                .Append(" — dropping that prefab into the scene sets up the whole system.");

            Debug.LogWarning(message.ToString());
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void CollectDragVisualIssues(List<string> issues)
        {
            // AutoFindInstance, not AutoCreateInstance: asking the question must not create the
            // answer, or the check would report a healthy scene it just fixed for itself.
            var presenter = DragVisualPresenter.AutoFindInstance;
            if (presenter == null)
            {
                issues.Add(
                    "No DragVisualPresenter in the scene. Dragging still works, but nothing is drawn " +
                    "under the cursor and no auto-transfer flight animation plays.");
                return;
            }

            if (presenter.DefaultDragVisualPrefab == null)
            {
                issues.Add(
                    $"DragVisualPresenter '{presenter.name}' has no Default Drag Visual Prefab assigned. " +
                    "Drags starting from an inventory without its own InventoryDragVisualBinder will " +
                    "render nothing at all.");
            }

            if (presenter.PresentationCanvas == null)
            {
                issues.Add(
                    $"DragVisualPresenter '{presenter.name}' has no Canvas assigned. Drag visuals fall " +
                    "back to raw screen coordinates, which only lines up on a Screen Space - Overlay canvas.");
                return;
            }

            CollectCanvasOrderIssues(presenter.PresentationCanvas.rootCanvas, issues);
        }

        /// <summary>
        /// The drag visual has to draw above the inventories it is dragged over. Only a strictly
        /// higher sorting order elsewhere is reported: ties are resolved by hierarchy order, which
        /// is a legitimate setup, and flagging those would turn this warning into noise.
        /// </summary>
        private static void CollectCanvasOrderIssues(Canvas dragCanvas, List<string> issues)
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == dragCanvas ||
                    canvas != canvas.rootCanvas ||
                    canvas.sortingLayerID != dragCanvas.sortingLayerID ||
                    canvas.sortingOrder <= dragCanvas.sortingOrder)
                    continue;

                issues.Add(
                    $"Canvas '{canvas.name}' (sorting order {canvas.sortingOrder}) draws above the drag " +
                    $"canvas '{dragCanvas.name}' (sorting order {dragCanvas.sortingOrder}), so the dragged " +
                    "item disappears behind it. The drag canvas needs the highest sorting order in the scene.");
            }
        }

        private static void CollectInputIssues(List<string> issues)
        {
            if (InputEventRouter.AutoFindInstance == null)
            {
                issues.Add(
                    "No InputEventRouter in the scene. Slots receive no clicks, drags, hotkeys or " +
                    "gamepad navigation.");
            }

#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            // The legacy path creates a missing EventSystem itself (InputEventRouter.EnsureLegacyEventSystemReady),
            // so an absent one is only a real problem when the Input System drives the UI.
            if (EventSystem.current == null &&
                Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                issues.Add(
                    "No EventSystem in the scene. Unity delivers no pointer events at all, so nothing is " +
                    "draggable. Add one via GameObject > UI > Event System.");
            }
#endif
        }
#endif
    }
}
