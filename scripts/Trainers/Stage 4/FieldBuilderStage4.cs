using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public partial class FieldBuilderStage4 : Node2D
{
    [Export] public PackedScene WallScene;
    [Export] public PackedScene BoundsScene;
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene BallScene;

    // Field dimensions (meters)
    [Export] public float Width = 90;
    [Export] public float Height = 45f;

    [Export] public float BoundsOffset = 5f;

    // Physical size of the wall prefab at Scale = (1,1)
    private const float WallPrefabSize = 1f;

    public int TeamSize;
    public List<RigidBody2D> Players = new List<RigidBody2D>();
    public List<AIPlayerStage4> PlayersData = new List<AIPlayerStage4>();
    public RigidBody2D Ball;

    public AgentStage4 Agent;

    private float prevDistToBall = -1;

    [Export] public PackedScene StatsScene;
    private RichTextLabel statsLabel;

    private int leftGoals = 0;
    private int rightGoals = 0;

    private float ballSpawnTime = 2f;
    private float ballSpawnTimer;

    private int Step = 0;
    private int frameCount = 0;

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

        CreateWall(
            WallScene,
            new Vector2(-(Width / 2 + WallPrefabSize / 2), 0),
            new Vector2(WallPrefabSize, Height));

        CreateWall(
            WallScene,
            new Vector2(Width / 2 + WallPrefabSize / 2, 0),
            new Vector2(WallPrefabSize, Height));

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

        for(var i = 0; i < TeamSize; i++)
        {
            var player = Spawn(PlayerScene);
            player.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.Cyan;
            Players.Add(player);
            PlayersData.Add(new AIPlayerStage4());
        }

        Ball = Spawn(BallScene);
        Ball.LinearDamp = (float)GD.RandRange(0.05f, Ball.LinearDamp);

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
        ballSpawnTimer = ballSpawnTime;

        ResetState();
    }

    public override void _Process(double delta)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        statsLabel.Text = $"{Agent.Fitness:F1}";
    }

    public override void _PhysicsProcess(double delta)
    {
        var ClosestToBallPlayerIndex = CalcBallRanks();

        frameCount++;
        if(frameCount >= 5)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                var actions = AgentObserve(i);
                AgentAct(actions, i);
            }
            frameCount = 0;
        }

        ResetPositions(ClosestToBallPlayerIndex);
        CalcFitness(ClosestToBallPlayerIndex);

        CheckBallInBounds();
    }

    private void CreateWall(PackedScene scene, Vector2 positionMeters, Vector2 sizeMeters)
    {
        var wall = scene.Instantiate<Node2D>();

        wall.Position = WorldParameters.Meters(positionMeters);

        wall.Scale = new Vector2(
            sizeMeters.X / WallPrefabSize,
            sizeMeters.Y / WallPrefabSize
        );

        AddChild(wall);
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

    private void SetObjectToRandomPosition(RigidBody2D obj)
    {
        float margin = .5f;

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

    private void SetObjectToRandomVelocity(RigidBody2D obj)
    {
        obj.ApplyCentralImpulse(
            new Vector2(WorldParameters.RandomNP(), WorldParameters.RandomNP()) * WorldParameters.UnitsPerMeter * 20);
    }

    private void ResetState()
    {
        var pos = Positions.GetPositions(Step);
        SetPosition(Ball, pos.ballPos);

        if(Step == 0)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                SetPosition(Players[i], pos.PlayersPositions[i]);
            }
        }

        Step++;
    }

    private void ResetPositions(int playerIndex)
    {
        if (((Player)Players[playerIndex])._ball != null)
        {
            if (ballSpawnTimer <= 0)
            {
                ResetState();

                Agent.Fitness += 100f;
                ballSpawnTimer = ballSpawnTime;
                prevDistToBall = -1;
            }
            ballSpawnTimer -= (float)GetPhysicsProcessDeltaTime();

        }
    }

    private float[] AgentObserve(int playerIndex)
    {
        List<float> inputs = new();
        var Player = Players[playerIndex];

        // Ball position relative to player (local space)
        Vector2 localBall = (Ball.Position - Player.Position).Rotated(-Player.Rotation);
        Vector2 dirToBall = localBall.Normalized();

        float maxDistance = new Vector2(Width, Height).Length();
        float ballDistance = WorldParameters.ToMeters(localBall).Length();

        inputs.Add(dirToBall.X);
        inputs.Add(dirToBall.Y);
        inputs.Add(ballDistance / maxDistance);
        inputs.Add(1f - (Mathf.Clamp(ballDistance, 0f, 5f) / 5f));

        // Ball velocity (local space)
        Vector2 localBallVelocity =
            WorldParameters.ToMeters(Ball.LinearVelocity).Rotated(-Player.Rotation) / 60f;

        inputs.Add(localBallVelocity.X);
        inputs.Add(localBallVelocity.Y);

        // Player velocity (local space)
        Vector2 localPlayerVelocity =
            WorldParameters.ToMeters(Player.LinearVelocity).Rotated(-Player.Rotation) / 10f;

        inputs.Add(localPlayerVelocity.X);
        inputs.Add(localPlayerVelocity.Y);

        // Player angular velocity
        inputs.Add(Player.AngularVelocity / ((Player)Player).MaxAngularVelocity);

        // Stamina
        inputs.Add(((Player)Player).CurrentStamina);

        //Pos
        inputs.Add(Player.Position.X / WorldParameters.Meters(Width / 2f));
        inputs.Add(Player.Position.Y / WorldParameters.Meters(Height / 2f));

        // Left goal (local)
        inputs.Add(0);
        inputs.Add(0);
        inputs.Add(0);

        // Right goal (local)
        inputs.Add(0);
        inputs.Add(0);
        inputs.Add(0);

        // Match state
        inputs.Add(0);
        inputs.Add(0);

        inputs.Add(Ball.Position.X / WorldParameters.Meters(Width / 2f));
        inputs.Add(Ball.Position.Y / WorldParameters.Meters(Height / 2f));

        inputs.Add(PlayersData[playerIndex].BallRank);
        inputs.Add(0);

        inputs.Add(((Ball)Ball).Spin);

        //Memory
        for (int i = 0; i < TrainingParameters.MemorySize; i++)
            inputs.Add(0);

        // Teammates (local)
        for (int i = 0; i < TeamSize; i++)
        {
            if (i == playerIndex)
                continue;

            var teammate = Players[i];

            var playerToTeammateVec = WorldParameters.ToMeters(Players[i].Position - Players[playerIndex].Position).Rotated(-Players[playerIndex].Rotation);
            var dirToTeammate = playerToTeammateVec.Normalized();

            inputs.Add(dirToTeammate.X);
            inputs.Add(dirToTeammate.Y);
            inputs.Add(playerToTeammateVec.Length() / maxDistance);
            inputs.Add(1 - (Mathf.Clamp(playerToTeammateVec.Length(), 0, 5) / 5));

            var teammateVel = WorldParameters.ToMeters(Players[i].LinearVelocity).Rotated(-Players[playerIndex].Rotation) / WorldParameters.PlayerSpeed;
            inputs.Add(teammateVel.X);
            inputs.Add(teammateVel.Y);

            var teammateToBall = WorldParameters.ToMeters(Ball.Position - Players[i].Position);
            inputs.Add(1 - (Mathf.Clamp(teammateToBall.Length(), 0, 10) / 10));
        }

        return Agent.Brain.forward(inputs.ToArray());
    }

    private void AgentAct(float[] actions, int playerIndex)
    {
        var playerScript = (Player)Players[playerIndex];
        playerScript.isAi = true;

        playerScript.Move.X = actions[0];
        playerScript.Move.Y = actions[1];
        playerScript.RotateBody = actions[2];
    }

    private void CalcFitness(int playerIndex)
    {
        var dist = WorldParameters.ToMeters(Ball.Position - Players[playerIndex].Position).Length();
        if (prevDistToBall > 0)
            Agent.Fitness += prevDistToBall - dist;
        prevDistToBall = dist;
    }

    private int GetClosestToBallPlayer()
    {
        int closestIndex = 0;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < Players.Count; i++)
        {
            float distance = WorldParameters.ToMeters((Ball.Position - Players[i].Position).LengthSquared());
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    private int CalcBallRanks()
    {
        var sorted = Players
            .Select((player, index) => new
            {
                Index = index,
                Dist = WorldParameters.ToMeters((Ball.Position - player.Position).LengthSquared())
            })
            .OrderBy(x => x.Dist)
            .ToList();

        for (int rank = 0; rank < sorted.Count; rank++)
        {
            float value = 1f - (rank / (float)(TeamSize - 1));
            PlayersData[sorted[rank].Index].BallRank = value;
        }
        return sorted[0].Index;
    }
}

public class AIPlayerStage4
{
    public float BallRank;
}