using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    [CreateAssetMenu(menuName = "DragAndDrop/Examples/Minecraft/Item Database")]
    public class MinecraftItemDatabase : ScriptableObject
    {
        [SerializeField] private MinecraftItemSO[] _items;

        private Dictionary<string, MinecraftItemSO> _lookup;

        public MinecraftItemSO GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, MinecraftItemSO>();
                foreach (var item in _items)
                {
                    if (item != null)
                        _lookup[item.ItemId] = item;
                }
            }

            return _lookup.TryGetValue(itemId, out var result) ? result : null;
        }
    }
}
