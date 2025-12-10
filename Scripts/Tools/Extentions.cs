#if ENABLE_REFLEX_DI
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
#endif
using UnityEngine;

namespace DragAndDropSystem.Tools
{
    public static class Extentions
    {
        
#if ENABLE_REFLEX_DI
        public static T Instantiate<T>(this Container container, T prefab, Transform parent = null) where T : Component
        {
            T instance = container.InstantiateNonActive(prefab, parent);
            instance.gameObject.SetActive(true);
            return instance;
        }
        public static T InstantiateNonActive<T>(this Container container, T prefab, Transform parent = null) where T : Component
        {
            bool isActive = prefab.gameObject.activeSelf;
            prefab.gameObject.SetActive(false);
            T instance = Object.Instantiate(prefab, parent);
            AttributeInjector.Inject(instance, container);
            prefab.gameObject.SetActive(isActive);
            return instance;
        }
#endif
        
        public static void DragAndDropLog(string message)
        {
            //return;
            Debug.Log($"<color=cyan>[DragAndDropSystem]</color> {message}");
        }
    }
}