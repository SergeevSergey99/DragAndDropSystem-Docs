using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Examples.Trading
{

    /// <summary>
    /// Стек торговых предметов (предмет + количество)
    /// </summary>
    [Serializable]
    public class TradableItemModel
    {
        public TradableItemSO originalSO;
        public DateTime GetTimestamp;

        public TradableItemModel(TradableItemSO originalSo)
        {
            originalSO = originalSo;
            GetTimestamp = DateTime.Now;
        }
    }
}
