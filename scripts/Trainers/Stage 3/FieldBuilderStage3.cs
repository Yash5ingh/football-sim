using Godot;
using System;
using System.Collections.Generic;

public partial class FieldBuilderStage3 : Node2D
{
    [Export] public PackedScene WallScene;
    [Export] public PackedScene GoalScene;
    [Export] public PackedScene BoundsScene;
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene BallScene;

    // Field dimensions (meters)
    [Export] public float Width = 90;
    [Export] public float Height = 45f;
    [Export] public float GoalHeight = 8f;

    [Export] public float BoundsOffset = 5f;

    // Physical size of the wall prefab at Scale = (1,1)
    private const float WallPrefabSize = 1f;

    public RigidBody2D Player;
    public RigidBody2D Ball;
    public Area2D LeftGoal;
    public Area2D RightGoal;

    public AgentStage3 Agent;

    public int NumOpponents;
    public int currActiveOpponents;
    private int actualActiveOpponents;
    private List<Dummy> Opponents = new List<Dummy>();

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


        var goalWallHeight = (Height - (GoalHeight)) / 2;
        var goalWallPos = (GoalHeight + goalWallHeight) / 2;

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
            new Vector2(-(Width / 2 + WallPrefabSize / 2) - WallPrefabSize/2, 0),
            new Vector2(WallPrefabSize, GoalHeight));
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
            new Vector2((Width / 2 + WallPrefabSize / 2) + WallPrefabSize/2, 0),
            new Vector2(WallPrefabSize, GoalHeight));
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

        ((Player)Player).isAi = true;

        for(int i = 0; i < NumOpponents; i++)
        {
            var opp = new Dummy();
            opp.Body = Spawn(PlayerScene);
            opp.Body.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.Red;
            opp.Script = (Player)opp.Body;
            opp.Script.isAi = true;
            opp.aggressiveness = GD.Randf();
            if (GD.Randf() < ((float)currActiveOpponents / NumOpponents))
            {
                actualActiveOpponents++;
                opp.Active = true;
            }
            Opponents.Add(opp);
        }

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
    }

    public override void _Process(double delta)
    {
        UpdateStats();
    }

    public override void _PhysicsProcess(double delta)
    {
        var actions = AgentObserve();
        AgentAct(actions);

        UpdateDummies();

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

    private void UpdateStats()
    {
        statsLabel.Text = $"{leftGoals}-{rightGoals}\t{Agent.Fitness:F2}";
    }

    private void CheckBallInBounds()
    {
        var rightBoundTrigger = WorldParameters.Meters((Width + BoundsOffset) / 2);
        var rightBound = WorldParameters.Meters((Width) / 2);
        if (Ball.Position.X > rightBoundTrigger)
            SetPosition(Ball, new Vector2(rightBound - WorldParameters.UnitsPerMeter, Ball.Position.Y));

        if (Ball.Position.X < -rightBoundTrigger)
            SetPosition(Ball, new Vector2(-rightBound + WorldParameters.UnitsPerMeter, Ball.Position.Y));

        var upperBoundTrigger = WorldParameters.Meters((Height + BoundsOffset) / 2);
        var upperBound = WorldParameters.Meters((Height) / 2);
        if (Ball.Position.Y > upperBoundTrigger)
            SetPosition(Ball, new Vector2(Ball.Position.X, upperBound - WorldParameters.UnitsPerMeter));

        if (Ball.Position.Y < -upperBoundTrigger)
            SetPosition(Ball, new Vector2(Ball.Position.X, -upperBound + WorldParameters.UnitsPerMeter));
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
        SetObjectToRandomPosition(Player);
        SetObjectToRandomPosition(Ball);

        actualActiveOpponents = 0;
        foreach (var opp in Opponents)
        {
            SetObjectToRandomPosition(opp.Body);

            if (GD.Randf() < ((float)currActiveOpponents / NumOpponents))
            {
                actualActiveOpponents++;
                opp.Active = true;
            }
            else
            {
                opp.Active = false;
            }
        }

        prevDistToGoal = -1;
    }

    private void OnLeftGoal(Node2D body)
    {
        if (body == Ball)
        {
            rightGoals++;
            Agent.Fitness -= 200f / ((float)actualActiveOpponents + 1f);
            ResetPositions();
        }
    }

    private void OnRightGoal(Node2D body)
    {
        if (body == Ball)
        {
            leftGoals++;
            Agent.Fitness += 100f * ((float)actualActiveOpponents + 1f);
            ResetPositions();
        }
    }

    private float[] AgentObserve()
    {
        var inputs = new List<float>();

        float maxDistance = new Vector2(Width, Height).Length();

        // Ball
        var playerToBallVec = WorldParameters.ToMeters(Ball.Position - Player.Position);
        var dirToBall = playerToBallVec.Normalized();
        var disToBallFar = playerToBallVec.Length() / maxDistance;
        var disToBallNear = 1 - (Mathf.Clamp(playerToBallVec.Length(), 0, 5) / 5);

        inputs.Add(dirToBall.X);
        inputs.Add(dirToBall.Y);
        inputs.Add(disToBallFar);
        inputs.Add(disToBallNear);

        // Ball velocity
        var ballVel = WorldParameters.ToMeters(Ball.LinearVelocity) / WorldParameters.BallSpeed;
        inputs.Add(ballVel.X);
        inputs.Add(ballVel.Y);

        // Player velocity
        var playerVel = WorldParameters.ToMeters(Player.LinearVelocity) / WorldParameters.PlayerSpeed;
        inputs.Add(playerVel.X);
        inputs.Add(playerVel.Y);

        // Player stamina
        inputs.Add(((Player)Player).CurrentStamina);

        inputs.Add(Player.Position.X / WorldParameters.Meters(Width / 2f));
        inputs.Add(Player.Position.Y / WorldParameters.Meters(Height / 2f));

        // Left goal
        var playerToLeftGoalVec = WorldParameters.ToMeters(LeftGoal.Position - Player.Position);
        var dirToLeftGoal = playerToLeftGoalVec.Normalized();

        inputs.Add(dirToLeftGoal.X);
        inputs.Add(dirToLeftGoal.Y);
        inputs.Add(playerToLeftGoalVec.Length() / maxDistance);

        // Right goal
        var playerToRightGoalVec = WorldParameters.ToMeters(RightGoal.Position - Player.Position);
        var dirToRightGoal = playerToRightGoalVec.Normalized();

        inputs.Add(dirToRightGoal.X);
        inputs.Add(dirToRightGoal.Y);
        inputs.Add(playerToRightGoalVec.Length() / maxDistance);

        // Opponents
        for (int i = 0; i < NumOpponents; i++)
        {
            var opp = Opponents[i];

            var playerToOppVec = WorldParameters.ToMeters(opp.Body.Position - Player.Position);
            var dirToOpp = playerToOppVec.Normalized();
            var disToOppFar = playerToOppVec.Length() / maxDistance;
            var disToOppNear = 1 - (Mathf.Clamp(playerToOppVec.Length(), 0, 5) / 5);

            inputs.Add(dirToOpp.X);
            inputs.Add(dirToOpp.Y);
            inputs.Add(disToOppFar);
            inputs.Add(disToOppNear);

            var oppVel = WorldParameters.ToMeters(opp.Body.LinearVelocity) / WorldParameters.PlayerSpeed;
            inputs.Add(oppVel.X);
            inputs.Add(oppVel.Y);
        }

        return Agent.Brain.forward(inputs.ToArray());
    }

    private void AgentAct(float[] actions)
    {
        var playerScript = (Player)Player;

        playerScript.Move.X = actions[0];
        playerScript.Move.Y = actions[1];
        playerScript.KickDir.X = actions[2];
        playerScript.KickDir.Y = actions[3];
    }

    private void CalcFitness()
    {
        if((Player.Position-Ball.Position).Length() < WorldParameters.Meters(1))
        {
            var dist = WorldParameters.ToMeters(Ball.Position - RightGoal.Position).Length();
            if (prevDistToGoal > 0)
                Agent.Fitness += (prevDistToGoal - dist) / Width * (actualActiveOpponents + 1) * 10;
            prevDistToGoal = dist;
        }
    }


    private void UpdateDummies()
    {
        foreach (var dummy in Opponents)
        {
            Vector2 leftGoal = LeftGoal.Position;
            Vector2 ball = Ball.Position;
            if (dummy.Active)
            {
                Vector2 rightGoal = RightGoal.Position + new Vector2(WorldParameters.RandomNP(), WorldParameters.RandomNP()) * WorldParameters.Meters(GoalHeight);
                var agro = dummy.aggressiveness;
                if ((ball - rightGoal).LengthSquared() < (leftGoal - rightGoal).LengthSquared() * .1f)
                    agro = Mathf.Clamp(agro + .5f, 0f, 1f);
                Vector2 blockPoint = rightGoal + (ball - rightGoal) * agro;
                Vector2 move = GD.Randf() > agro ? blockPoint - dummy.Body.Position : ball - dummy.Body.Position;
                dummy.Script.Move = move.Normalized();
            }
            else
            {
                dummy.Script.Move = Vector2.Zero;
            }

            dummy.Script.KickDir = (leftGoal - ball).Normalized() + new Vector2(WorldParameters.RandomNP(), WorldParameters.RandomNP()) * (1-dummy.aggressiveness/2);
        }
    }

    class Dummy
    {
        public RigidBody2D Body;
        public Player Script;
        public float aggressiveness;
        public bool Active;
    }
}