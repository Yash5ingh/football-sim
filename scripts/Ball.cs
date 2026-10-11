using Godot;

public partial class Ball : RigidBody2D
{
    public ClubPlayer LastTouchedPlayer;
    public ClubPlayer PreviousTouchedPlayer;
    public Vector2 LastTouchPosition;

    public bool HomePossession = GD.Randf() < 0.5f;

    public float Spin = 0f;

    [Export]
    public float CurveAmount = 0.02f;

    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        if (Mathf.Abs(Spin) < 0.001f)
            return;

        Vector2 velocity = state.LinearVelocity;

        if (velocity.LengthSquared() < 0.01f)
            return;

        float speed = velocity.Length();

        float angle = Spin * CurveAmount * speed * (float)state.Step;

        state.LinearVelocity = velocity.Rotated(angle);
    }
}