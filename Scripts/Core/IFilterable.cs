namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Интерфейс для предметов, поддерживающих фильтрацию.
    /// Предметы могут реализовать этот интерфейс для предоставления данных фильтрации.
    /// </summary>
    public interface IFilterable
    {
        /// <summary>
        /// Категория предмета для фильтрации (например: "Weapon", "Armor", "Consumable")
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Подкатегория предмета (например: "Sword", "Bow", "Staff" для категории "Weapon")
        /// </summary>
        string Subcategory { get; }

        /// <summary>
        /// Редкость предмета для фильтрации (например: 0=Common, 1=Uncommon, 2=Rare, 3=Epic, 4=Legendary)
        /// </summary>
        int Rarity { get; }
    }

    /// <summary>
    /// Интерфейс для предметов, поддерживающих сортировку.
    /// </summary>
    public interface ISortable
    {
        /// <summary>
        /// Значение для сортировки (например: цена, уровень, вес)
        /// </summary>
        int SortValue { get; }

        /// <summary>
        /// Имя для алфавитной сортировки
        /// </summary>
        string SortName { get; }
    }
}
