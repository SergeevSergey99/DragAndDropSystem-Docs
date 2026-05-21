using System;
using System.Collections.Generic;
using UDND.Core;
using UnityEngine.Serialization;

namespace UDND.Examples.Trading
{

    /// <summary>
    /// Stack of tradable items (item + amount)
    /// </summary>
    [Serializable]
    public class TradableItemModel
    {
        public TradableItemSO originalSO;
        public string GetTimestamp;

        public TradableItemModel()
        {
            originalSO = null;
            GetTimestamp = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString();
        }
        public TradableItemModel(TradableItemSO originalSo)
        {
            originalSO = originalSo;
            GetTimestamp = DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString();
        }
    }
}
