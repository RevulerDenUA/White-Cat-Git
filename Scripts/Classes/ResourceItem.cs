using Godot;
using System;

public class ResourceItem
{
    public string Name;
    public int Amount;

    public ResourceItem(string name, int amount)
    {
        Name = name;
        Amount = amount;
    }

    public void AddAmount(int amount)
    {
        Amount += amount;
    }
}