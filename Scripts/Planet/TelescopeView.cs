using Godot;

// Телескоп шатла: показує зоряну систему, вибір планети й апарата для висадки.
// Контракт для термінала (command_room.gd):
//   • сцена res://Scene/Telescope.tscn, корінь — цей Control (сам розтягується на батька);
//   • сигнал Closed — гравець вийшов із телескопа назад у термінал;
//   • клавіші приймає в _Input, коли видимий (термінал має пересилати ввід у дисплей).
public partial class TelescopeView : Control
{
	[Signal] public delegate void ClosedEventHandler();

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var label = new Label { Text = "Телескоп (заглушка). Backspace — назад до термінала." };
		AddChild(label);
	}

	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree()) return;
		if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Backspace })
		{
			GetViewport().SetInputAsHandled();
			EmitSignal(SignalName.Closed);
		}
	}
}
