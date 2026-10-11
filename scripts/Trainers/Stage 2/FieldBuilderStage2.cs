using Godot;
using System.Collections.Generic;

public partial class FieldBuilderStage2 : Node2D
{
    [Export] public PackedScene WallScene;
    [Export] public PackedScene GoalScene;
    [Export] public PackedScene BoundsScene;
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene BallScene;

    // Field dimensions (meters)
    [Export] public float Width = 90;
    [Export] public float Height = 45f;
    [Export] public float GoalHeight = 15f;

    [Export] public float BoundsOffset = 5f;

    // Physical size of the wall prefab at Scale = (1,1)
    private const float WallPrefabSize = 1f;

    public RigidBody2D Player;
    public RigidBody2D Ball;
    public Area2D LeftGoal;
    public Area2D RightGoal;

    public AgentStage2 Agent;

    private float prevDistToGoal = -1;

    [Export] public PackedScene StatsScene;
    private RichTextLabel statsLabel;

    private int leftGoals = 0;
    private int rightGoals = 0;

    public override void _Ready()
    {
        // Inner walls
        CreateWall(
            WallScene,
            new Vector2(0, -(Height / 2 + WallPrefabSize / 2)),
            new Vector2(Width + 2 * WallPrefabSize, WallPrefabSize));

        CreateWall(
            WallScene,
            new Vector2(0, Height / 2 + WallPrefabSize / 2),
            new Vector2(Width + 2 * WallPrefabSize, WallPrefabSize));


        var goalWallHeight = (Height) / 2;
        var goalWallPos = (goalWallHeight) / 2;

        CreateWall(
            WallScene,
            new Vector2(-(Width / 2 + WallPrefabSize / 2), goalWallPos),
            new Vector2(WallPrefabSize, goalWallHeight));

        CreateWall(
            WallScene,
            new Vector2(-(Width / 2 + WallPrefabSize / 2), -goalWallPos),
            new Vector2(WallPrefabSize, goalWallHeight));

        LeftGoal = (Area2D)CreateWall(
            GoalScene,
            new Vector2(-(Width / 2 + WallPrefabSize / 2) - WallPrefabSize /2, 0),
            new Vector2(WallPrefabSize, WallPrefabSize));
        LeftGoal.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.Cyan;

        CreateWall(
            WallScene,
            new Vector2((Width / 2 + WallPrefabSize / 2), goalWallPos),
            new Vector2(WallPrefabSize, goalWallHeight));

        CreateWall(
            WallScene,
            new Vector2((Width / 2 + WallPrefabSize / 2), -goalWallPos),
            new Vector2(WallPrefabSize, goalWallHeight));

        RightGoal = (Area2D)CreateWall(
            GoalScene,
            new Vector2((Width / 2 + WallPrefabSize / 2) + WallPrefabSize / 2, 0),
            new Vector2(WallPrefabSize, WallPrefabSize));
        RightGoal.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.Red;

        // Outer bounds
        float outerWidth = Width + 2 * BoundsOffset;
        float outerHeight = Height + 2 * BoundsOffset;

        CreateWall(
            BoundsScene,
            new Vector2(0, -(outerHeight / 2 + WallPrefabSize / 2)),
            new Vector2(outerWidth + 2 * WallPrefabSize, WallPrefabSize));

        CreateWall(
            BoundsScene,
            new Vector2(0, outerHeight / 2 + WallPrefabSize / 2),
            new Vector2(outerWidth + 2 * WallPrefabSize, WallPrefabSize));

        CreateWall(
            BoundsScene,
            new Vector2(-(outerWidth / 2 + WallPrefabSize / 2), 0),
            new Vector2(WallPrefabSize, outerHeight));

        CreateWall(
            BoundsScene,
            new Vector2(outerWidth / 2 + WallPrefabSize / 2, 0),
            new Vector2(WallPrefabSize, outerHeight));

        LeftGoal.BodyEntered += OnLeftGoal;
        RightGoal.BodyEntered += OnRightGoal;

        Player = Spawn(PlayerScene);
        Player.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.Cyan;
        Ball = Spawn(BallScene);

        var stats = StatsScene.Instantiate<Node2D>();
        stats.Position = WorldParameters.Meters(
            new Vector2(0, -(Height / 2 + BoundsOffset / 2) - WallPrefabSize)
        );
        statsLabel = stats.GetNode<RichTextLabel>("RichTextLabel");
        statsLabel.Position = WorldParameters.Meters(
            new Vector2(-(Width + 2 * BoundsOffset) / 2, -BoundsOffset / 2)
        );
        statsLabel.Size = WorldParameters.Meters(
            new Vector2(Width + 2 * BoundsOffset, BoundsOffset)
        );
        AddChild(stats);
        ResetPositions();
    }

    public override void _Process(double delta)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        statsLabel.Text = $"{leftGoals}-{rightGoals}\t{Agent.Fitness:F1}";
    }

    public override void _PhysicsProcess(double delta)
    {
        var actions = AgentObserve();
        AgentAct(actions);

        CalcFitness();

        CheckBallInBounds();
    }

    private Node2D CreateWall(PackedScene scene, Vector2 positionMeters, Vector2 sizeMeters)
    {
        var wall = scene.Instantiate<Node2D>();

        wall.Position = WorldParameters.Meters(positionMeters);

        wall.Scale = new Vector2(
            sizeMeters.X / WallPrefabSize,
            sizeMeters.Y / WallPrefabSize
        );

        AddChild(wall);
        return wall;
    }

    private RigidBody2D Spawn(PackedScene scene)
    {
        var obj = scene.Instantiate<RigidBody2D>();

        SetObjectToRandomPosition(obj);

        AddChild(obj);
        return obj;
    }

    private void CheckBallInBounds()
    {
        var rightBound = WorldParameters.Meters((Width + BoundsOffset) / 2);
        if (Ball.Position.X > rightBound)
            SetPosition(Ball, new Vector2(rightBound - WorldParameters.UnitsPerMeter, Ball.Position.Y));

        var leftBound = -rightBound;
        if (Ball.Position.X < leftBound)
            SetPosition(Ball, new Vector2(leftBound + WorldParameters.UnitsPerMeter, Ball.Position.Y));

        var upperBound = WorldParameters.Meters((Height + BoundsOffset) / 2);
        if (Ball.Position.Y > upperBound)
            SetPosition(Ball, new Vector2(Ball.Position.X, upperBound - WorldParameters.UnitsPerMeter));

        var lowerBound = -upperBound;
        if (Ball.Position.Y < lowerBound)
            SetPosition(Ball, new Vector2(Ball.Position.X, lowerBound + WorldParameters.UnitsPerMeter));
    }

    private void SetObjectToRandomPosition(RigidBody2D obj)
    {
        float margin = 2f;

        float x = (float)GD.RandRange(
            -(Width / 2 - margin),
             Width / 2 - margin);

        float y = (float)GD.RandRange(
            -(Height / 2 - margin),
             Height / 2 - margin);

        // This is the local position relative to this specific field
        Vector2 localTargetPos = WorldParameters.Meters(new Vector2(x, y));

        SetPosition(obj, localTargetPos);
    }

    private void SetObjectToRandomPosition(Area2D obj)
    {
        float margin = 2f;

        float x = (float)GD.RandRange(
            -(Width / 2 - margin),
             Width / 2 - margin);

        float y = (float)GD.RandRange(
            -(Height / 2 - margin),
             Height / 2 - margin);

        Vector2 localTargetPos = WorldParameters.Meters(new Vector2(x, y));
        obj.Position = localTargetPos;
    }

    private void SetPosition(RigidBody2D obj, Vector2 localTargetPos)
    {
        // Convert to a global position so it maps to the correct field instance
        Vector2 globalTargetPos = ToGlobal(localTargetPos);

        // 1. Reset node properties using local position
        obj.LinearVelocity = Vector2.Zero;
        obj.AngularVelocity = 0f;
        obj.Position = localTargetPos;

        // 2. Force-inject state changes using global position
        var state = PhysicsServer2D.BodyGetDirectState(obj.GetRid());
        if (state != null)
        {
            state.LinearVelocity = Vector2.Zero;
            state.AngularVelocity = 0f;

            Transform2D transform = state.Transform;
            transform.Origin = globalTargetPos; // Must be global!
            state.Transform = transform;
        }
    }

    private void SetObjectToRandomVelocity(RigidBody2D obj)
    {
        obj.ApplyCentralImpulse(
            new Vector2(WorldParameters.RandomNP(), WorldParameters.RandomNP()) * WorldParameters.UnitsPerMeter * 20);    
    }

    private void ResetPositions()
    {
        SetObjectToRandomPosition(LeftGoal);
        SetObjectToRandomPosition(RightGoal);

        prevDistToGoal = -1f;
    }
    
    private void OnLeftGoal(Node2D body)
    {
        if (body == Ball)
        {
            Agent.Fitness -= 5;
            rightGoals++;
            ResetPositions();
        }
    }

    private void OnRightGoal(Node2D body)
    {
        if (body == Ball)
        {
            Agent.Fitness += 5;
            leftGoals++;
            ResetPositions();
        }
    }

    private float[] AgentObserve()
    {
        List<float> inputs = new();

        float maxDistance = new Vector2(Width, Height).Length();

        // Ball position (local)
        Vector2 localBall = WorldParameters.ToMeters(Ball.Position - Player.Position).Rotated(-Player.Rotation);
        Vector2 dirToBall = localBall.Normalized();

        inputs.Add(dirToBall.X);
        inputs.Add(dirToBall.Y);
        inputs.Add(localBall.Length() / maxDistance);
        inputs.Add(1f - (Mathf.Clamp(localBall.Length(), 0f, 5f) / 5f));

        // Ball velocity (local)
        Vector2 localBallVel = WorldParameters.ToMeters(Ball.LinearVelocity).Rotated(-Player.Rotation) / 60f;
        inputs.Add(localBallVel.X);
        inputs.Add(localBallVel.Y);

        // Player velocity (local)
        Vector2 localPlayerVel = WorldParameters.ToMeters(Player.LinearVelocity).Rotated(-Player.Rotation) / 10f;
        inputs.Add(localPlayerVel.X);
        inputs.Add(localPlayerVel.Y);

        // Player angular velocity
        inputs.Add(Player.AngularVelocity / ((Player)Player).MaxAngularVelocity);

        // Stamina
        inputs.Add(((Player)Player).CurrentStamina);

        //Pos
        inputs.Add(Player.Position.X / WorldParameters.Meters(Width / 2f));
        inputs.Add(Player.Position.Y / WorldParameters.Meters(Height / 2f));

        // Left goal (local)
        Vector2 localLeftGoal = WorldParameters.ToMeters(LeftGoal.Position - Player.Position).Rotated(-Player.Rotation);
        Vector2 dirLeftGoal = localLeftGoal.Normalized();

        inputs.Add(dirLeftGoal.X);
        inputs.Add(dirLeftGoal.Y);
        inputs.Add(localLeftGoal.Length() / maxDistance);

        // Right goal (local)
        Vector2 localRightGoal = WorldParameters.ToMeters(RightGoal.Position - Player.Position).Rotated(-Player.Rotation);
        Vector2 dirRightGoal = localRightGoal.Normalized();

        inputs.Add(dirRightGoal.X);
        inputs.Add(dirRightGoal.Y);
        inputs.Add(localRightGoal.Length() / maxDistance);

        return Agent.Brain.forward(inputs.ToArray());
    }

    private void AgentAct(float[] actions)
    {
        var playerScript = (Player)Player;
        playerScript.isAi = true;

        playerScript.Move.X = actions[0];
        playerScript.Move.Y = actions[1];
        playerScript.RotateBody = actions[2];
        playerScript.KickDir.X = actions[3];
        playerScript.KickDir.Y = actions[4];
        playerScript.KickPower = actions[5];
        playerScript.BallSpin = actions[6];
    }

    private void CalcFitness()
    {
        var dist = 10 * WorldParameters.ToMeters(Ball.Position - RightGoal.Position).Length()/Width;
        if (prevDistToGoal > 0)
            Agent.Fitness += prevDistToGoal - dist;
        prevDistToGoal = dist;
    }
}