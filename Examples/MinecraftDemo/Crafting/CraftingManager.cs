using System.Collections.Generic;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Examples.Minecraft
{
    /// <summary>
    /// Оркестратор крафта. Следит за изменениями сетки 3x3,
    /// проверяет рецепты и управляет слотом результата.
    ///
    /// Настройка на сцене:
    /// 1. Добавить на объект с CraftingGridBinding или рядом
    /// 2. Прокинуть ссылки: _gridInventory, _gridBinding, _outputBinding, _recipes
    /// 3. Grid inventory: Fixed 9 слотов, SeparableStacks (чтобы каждый слот рецепта был независим)
    /// 4. Output inventory: Fixed 1 слот. Стратегия и drag amount
    ///    будут принудительно настроены CraftingOutputBinding'ом для выдачи всего результата разом.
    /// </summary>
    public class CraftingManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UniversalInventory _gridInventory;
        [SerializeField] private CraftingGridBinding _gridBinding;
        [SerializeField] private CraftingOutputBinding _outputBinding;

        [Header("Recipes")]
        [SerializeField] private CraftingRecipeSO[] _recipes;

        private bool _isConsuming;
        private CraftingRecipeSO _currentRecipe;

        private void OnEnable()
        {
            _gridBinding.OnGridChanged += OnGridChanged;
            _outputBinding.OnResultTaken += OnResultTaken;

            CheckRecipe();
        }

        private void OnDisable()
        {
            _gridBinding.OnGridChanged -= OnGridChanged;
            _outputBinding.OnResultTaken -= OnResultTaken;
        }

        private void Start()
        {
            CheckRecipe();
        }

        private void OnGridChanged()
        {
            // Во время списания ингредиентов промежуточные состояния игнорируем
            if (_isConsuming)
                return;

            CheckRecipe();
        }

        private void CheckRecipe()
        {
            IReadOnlyList<ISlot> slots = _gridInventory.Slots;

            foreach (var recipe in _recipes)
            {
                if (recipe != null && recipe.Matches(slots))
                {
                    _currentRecipe = recipe;
                    _outputBinding.SetResult(
                        new MinecraftItemAdapter(recipe.Result),
                        recipe.ResultCount
                    );
                    return;
                }
            }

            // Ни один рецепт не подошёл
            _currentRecipe = null;
            _outputBinding.ClearResult();
        }

        private void OnResultTaken()
        {
            if (_currentRecipe == null)
                return;

            ConsumeIngredients();
        }

        /// <summary>
        /// Убрать по 1 предмету из каждого занятого слота сетки.
        /// После списания — повторная проверка рецепта (если ингредиентов хватает, результат появится снова).
        /// </summary>
        private void ConsumeIngredients()
        {
            _isConsuming = true;

            var slots = _gridInventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    _gridInventory.TryRemoveItem(slots[i].Stack.Item, 1, i);
                }
            }

            _isConsuming = false;

            // Проверить, хватает ли ингредиентов ещё на один крафт
            CheckRecipe();
        }
    }
}
