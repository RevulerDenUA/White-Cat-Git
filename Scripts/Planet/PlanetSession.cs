using System.Collections.Generic;
using Godot;

// Висадка на планету: ставить шатл на рельєф, розставляє апарати й одразу передає
// керування вибраному в телескопі. Esc — назад у шатл (командну кімнату).
// Апарат у зоні стикування на момент виходу вважається «в шатлі», інакше лишається
// на поверхні там, де стоїть, і з'явиться там же при наступній висадці на цю планету.
public partial class PlanetSession : Node
{
	[Export] public CameraControl Controller;
	[Export] public TerrainWorld Terrain;
	[Export] public ShuttlePad Pad;
	[Export] public RemoteVehicle Rover;
	[Export] public RemoteVehicle Ingenuity;

	private readonly Dictionary<string, RemoteVehicle> _vehicles = new();
	private string _active = "";
	private string _status = "";
	private Label _line, _hint;
	private bool _leaving;

	public override void _Ready()
	{
		if (Rover != null) _vehicles[ShuttleState.RoverId] = Rover;
		if (Ingenuity != null) _vehicles[ShuttleState.IngenuityId] = Ingenuity;
		if (Controller != null) Controller.ExitRequested += ReturnToShuttle;
		BuildHud();
		// Терен будує шматки в _Ready; апарати ставимо, коли все вже в дереві
		CallDeferred(MethodName.Setup);
	}

	private void Setup()
	{
		_active = ShuttleState.PendingVehicle;
		if (string.IsNullOrEmpty(_active) || !_vehicles.ContainsKey(_active))
			_active = ShuttleState.RoverId; // сцену запустили напряму з редактора

		Pad?.PlaceOn(Terrain);

		foreach (var (id, adapter) in _vehicles)
		{
			if (!RemoteVehicle.IsLive(adapter) || !RemoteVehicle.IsLive(adapter.Vehicle)) continue;
			Node3D body = adapter.Vehicle;

			if (ShuttleState.TryGetOutside(id, out Transform3D saved))
			{
				Place(body, saved.Translated(Vector3.Up * 0.3f)); // лишився на поверхні
			}
			else if (id == _active)
			{
				Transform3D spawn = Pad.SpawnTransform(Terrain, id);
				Place(body, spawn);
				ShuttleState.SetOutside(id, spawn);
			}
			else
			{
				body.QueueFree(); // цей апарат у шатлі
			}
		}

		if (_vehicles.TryGetValue(_active, out RemoteVehicle chosen) && RemoteVehicle.IsLive(chosen.Vehicle))
		{
			Terrain.Player = chosen.Vehicle; // шматки рельєфу вантажаться навколо керованого апарата
			string error = Controller?.connect_target(_active) ?? "Немає контролера керування.";
			if (error != "") _status = error;
		}
		else _status = "Апарата немає в сцені.";
	}

	private static void Place(Node3D body, Transform3D xf)
	{
		body.GlobalTransform = xf;
		switch (body)
		{
			case RigidBody3D rb: rb.LinearVelocity = Vector3.Zero; rb.AngularVelocity = Vector3.Zero; break;
			case CharacterBody3D cb: cb.Velocity = Vector3.Zero; break;
		}
	}

	private void ReturnToShuttle()
	{
		if (_leaving) return;
		_leaving = true;
		foreach (var (id, adapter) in _vehicles)
		{
			if (!RemoteVehicle.IsLive(adapter) || !RemoteVehicle.IsLive(adapter.Vehicle)) continue;
			if (Pad != null && Pad.IsDocked(adapter.Vehicle)) ShuttleState.SetInside(id);
			else ShuttleState.SetOutside(id, adapter.Vehicle.GlobalTransform);
		}
		// після обробки Esc: зміна сцени одразу прибирає поточну з дерева
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, ShuttleState.ReturnScene);
	}

	// ---------- HUD ----------

	public override void _Process(double delta)
	{
		if (_line == null) return;
		string planet = CurrentGen.HasActivePlanet
			? $"{PlanetSelector.PlanetName(CurrentGen.ActivePlanet)} · {PlanetSelector.TypeName(CurrentGen.ActivePlanet.Type)}"
			: "Тестовий рельєф";

		string where = "";
		if (_vehicles.TryGetValue(_active, out RemoteVehicle a) && RemoteVehicle.IsLive(a.Vehicle) && Pad != null)
		{
			if (Pad.IsDocked(a.Vehicle)) where = "у шатлі";
			else
			{
				Vector3 d = a.Vehicle.GlobalPosition - Pad.GlobalPosition;
				d.Y = 0f;
				where = $"до шатла {d.Length():0} м";
			}
		}
		_line.Text = $"{ShuttleState.VehicleName(_active)} · {planet}" + (where != "" ? $" · {where}" : "");

		bool docked = where == "у шатлі";
		_hint.Text = _status != "" ? _status
			: docked ? "Esc — завершити висадку"
			: "Esc — до шатла (апарат лишиться тут)";
	}

	private void BuildHud()
	{
		var layer = new CanvasLayer { Layer = 5 };
		AddChild(layer);
		var box = new VBoxContainer
		{
			AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = 24f, OffsetTop = -76f, OffsetBottom = -20f,
			GrowVertical = Control.GrowDirection.Begin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		layer.AddChild(box);
		_line = HudLabel(20, new Color("eef0ec")); // WhiteCatTheme.WHITE
		_hint = HudLabel(16, new Color("a4b0b9")); // WhiteCatTheme.MUTED
		box.AddChild(_line);
		box.AddChild(_hint);
	}

	private static Label HudLabel(int size, Color color)
	{
		var l = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
		l.AddThemeColorOverride("font_outline_color", new Color("080e17")); // WhiteCatTheme.INK
		l.AddThemeConstantOverride("outline_size", 5);
		return l;
	}

	public override void _ExitTree()
	{
		if (Controller != null && IsInstanceValid(Controller)) Controller.ExitRequested -= ReturnToShuttle;
	}
}
