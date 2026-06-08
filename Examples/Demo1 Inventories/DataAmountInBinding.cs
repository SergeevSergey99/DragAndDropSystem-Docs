using System.Linq;
using UDND.Examples.General;
using UnityEngine;
using UnityEngine.UI;

namespace UDND.Examples
{
    public class DataAmountInBinding : MonoBehaviour
    {
        [SerializeField] private Text textAmount;
        [SerializeField] private ItemsSOInventoryDataBinding inventoryDataBinding;

        Color _color;
        private void Start()
        {
            _color = textAmount.color;
        }

        // Update is called once per frame
        void Update()
        {
            textAmount.text = $"Amount in Binding: {inventoryDataBinding.Items.Count}";
         
            textAmount.color = inventoryDataBinding.Items.Contains(null) ? Color.red : _color;
        }
    }
}
