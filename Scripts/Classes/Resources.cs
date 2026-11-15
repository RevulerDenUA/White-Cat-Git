using Godot;
using System;
using System.Collections.Generic;

public class Resources
{
    private readonly List<ResourceItem> _items = new List<ResourceItem>();

    // Список лише для читання: змінювати інвентар можна тільки через Add/Remove
    public IReadOnlyList<ResourceItem> Items => _items;

    // Пошук предмета за назвою (без урахування регістру). null, якщо немає.
    public ResourceItem Find(string name)
    {
        return _items.Find(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool Has(string name, int amount = 1)
    {
        var item = Find(name);
        return item != null && item.Amount >= amount;
    }

    // Кількість предмета (0, якщо немає)
    public int GetAmount(string name)
    {
        return Find(name)?.Amount ?? 0;
    }

    // Додає кількість; якщо предмета ще немає — створює
    public void Add(string name, int amount)
    {
        if (amount <= 0) return;

        var item = Find(name);
        if (item != null)
            item.AddAmount(amount);
        else
            _items.Add(new ResourceItem(name, amount));
    }

    // Віднімає кількість. Повертає false, якщо не вистачає (нічого не змінює).
    // Якщо кількість стала 0 — предмет видаляється зі списку.
    public bool Remove(string name, int amount)
    {
        if (amount <= 0) return false;

        var item = Find(name);
        if (item == null || item.Amount < amount) return false;

        item.AddAmount(-amount);
        if (item.Amount == 0)
            _items.Remove(item);
        return true;
    }

    // Повертає всі предмети як словник назва -> кількість
    public Dictionary<string, int> GetAll()
    {
        var result = new Dictionary<string, int>();
        foreach (var item in _items)
            result[item.Name] = item.Amount;
        return result;
    }

    // Чи вистачає всього зі списку (наприклад, вартість будівлі)
    public bool HasAll(IReadOnlyDictionary<string, int> cost)
    {
        foreach (var (name, amount) in cost)
            if (!Has(name, amount)) return false;
        return true;
    }

    // Чого саме не вистачає: назва -> скільки бракує
    public Dictionary<string, int> Missing(IReadOnlyDictionary<string, int> cost)
    {
        var result = new Dictionary<string, int>();
        foreach (var (name, amount) in cost)
        {
            int lack = amount - GetAmount(name);
            if (lack > 0) result[name] = lack;
        }
        return result;
    }

    // Списати все зі списку. Якщо чогось не вистачає — нічого не списує і повертає false.
    public bool RemoveAll(IReadOnlyDictionary<string, int> cost)
    {
        if (!HasAll(cost)) return false;
        foreach (var (name, amount) in cost)
            if (amount > 0) Remove(name, amount);
        return true;
    }
}
