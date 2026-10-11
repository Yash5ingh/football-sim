using Godot;

public partial class CameraController : Camera2D
{
    [Export] public float PanSpeed = 15000f;
    [Export] public float EdgeSize = 20f;

    [Export] public float ZoomSpeed = 0.1f;
    [Export] public float MinZoom = 0.2f;
    [Export] public float MaxZoom = 5f;
        
    private bool _dragging;
    private Vector2 _lastMouse;

    public override void _Ready()
    {
        MakeCurrent();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // ---------- Edge scrolling ----------
        Vector2 mouse = GetViewport().GetMousePosition();
        Vector2 size = GetViewportRect().Size;

        Vector2 move = Vector2.Zero;

        if (mouse.X < EdgeSize)
            move.X -= 1;

        if (mouse.X > size.X - EdgeSize)
            move.X += 1;

        if (mouse.Y < EdgeSize)
            move.Y -= 1;

        if (mouse.Y > size.Y - EdgeSize)
            move.Y += 1;

        //if (move != Vector2.Zero)
        //    Position += move.Normalized() * PanSpeed * 1000 * dt * Zoom.X;

        // ---------- Right mouse drag ----------
        if (_dragging)
        {
            Vector2 deltaMouse = mouse - _lastMouse;

            Position -= deltaMouse * PanSpeed / Zoom.X;

            _lastMouse = mouse;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right)
            {
                _dragging = mb.Pressed;
                _lastMouse = mb.Position;
            }

            if (mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.WheelDown)
                    SetZoom(Zoom * (1f - ZoomSpeed));

                if (mb.ButtonIndex == MouseButton.WheelUp)
                    SetZoom(Zoom * (1f + ZoomSpeed));
            }
        }
    }

    private void SetZoom(Vector2 zoom)
    {
        zoom.X = Mathf.Clamp(zoom.X, MinZoom, MaxZoom);
        zoom.Y = Mathf.Clamp(zoom.Y, MinZoom, MaxZoom);

        Zoom = zoom;
    }
}