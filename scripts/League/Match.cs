using Godot;
using System;
using System.Collections.Generic;

public partial class Match : Node2D
{
    [Export] public PackedScene WallScene;
    [Export] public PackedScene GoalScene;
    [Export] public PackedScene BoundsScene;
    [Export] public PackedScene PlayerScene;
    [Export] public PackedScene BallScene;


    [Export] public float Width = 90;
    [Export] public float Height = 45f;
    [Export] public float GoalHeight = 8f;

    [Export] public float BoundsOffset = 5f;

    private const float WallPrefabSize = 1f;

    public RigidBody2D Ball;
    public Area2D LeftGoal;
    public Area2D RightGoal;

    public Ball BallScript;

    public int TeamSize;

    public Club HomeTeam;
    public int HomeTeamRank;
    public List<AIPlayer> HomeTeamPlayers = new List<AIPlayer>();

    public Club AwayTeam;
    public int AwayTeamRank;
    public List<AIPlayer> AwayTeamPlayers = new List<AIPlayer>();

    [Export] public PackedScene StatsScene;
    private RichTextLabel statsLabel;

    public int leftGoals = 0;
    public int rightGoals = 0;

    public float TimeLeft;
    public float MatchTime;
    public string DisplayTime => $"{90 * (1 - (TimeLeft / MatchTime)):F2}";

    public override void _Ready()
    {
        Color awayTeamColor;
        Color awayTeamNumberColor;
        if (TeamColorGenerator.ColorDistance(HomeTeam.TeamColor, AwayTeam.TeamColor) < TeamColorGenerator.ColorDistance(HomeTeam.TeamColor, AwayTeam.AwayTeamColor))
        {
            awayTeamColor = AwayTeam.AwayTeamColor;
            awayTeamNumberColor = AwayTeam.AwayNumberColor;
        }
        else
        {
            awayTeamColor = AwayTeam.TeamColor;
            awayTeamNumberColor = AwayTeam.NumberColor;
        }

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
            new Vector2(-(Width / 2 + WallPrefabSize / 2) - WallPrefabSize / 2, 0),
            new Vector2(WallPrefabSize, GoalHeight));
        LeftGoal.GetNode<Sprite2D>("Sprite2D").Modulate = HomeTeam.TeamColor;

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
            new Vector2(WallPrefabSize, GoalHeight));
        RightGoal.GetNode<Sprite2D>("Sprite2D").Modulate = awayTeamColor;

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

        Ball = Spawn(BallScene);
        BallScript = (Ball)Ball;

        for (int i = 0; i < TeamSize; i++)
        {
            AIPlayer player = new AIPlayer();
            player.Body = Spawn(PlayerScene);
            player.Script = (Player)player.Body;
            player.Script.SetPlayerNumber(i + 1);
            player.Script.SetPlayerColor(HomeTeam.TeamColor);
            player.Script.SetPlayerNumberColor(HomeTeam.NumberColor);
            player.Script.Name = HomeTeam.Players[i].Name;
            player.Script.isAi = true;
            player.Active = true;
            player.Script.SetPlayerStats(HomeTeam.Players[i]);

            HomeTeamPlayers.Add(player);
        }

        for (int i = 0; i < TeamSize; i++)
        {
            AIPlayer opp = new AIPlayer();
            opp.Body = Spawn(PlayerScene);
            opp.Script = (Player)opp.Body;
            opp.Script.SetPlayerNumber(i + 1);
            opp.Script.SetPlayerColor(awayTeamColor);
            opp.Script.SetPlayerNumberColor(awayTeamNumberColor);
            opp.Script.isAi = true;
            opp.IsAwayTeam = true;
            opp.Active = true;
            opp.Script.SetPlayerStats(AwayTeam.Players[i]);

            AwayTeamPlayers.Add(opp);
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

        ResetPositions();
    }

    public override void _Process(double delta)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        statsLabel.Text = $"{HomeTeamRank}. {HomeTeam.TeamName}  {leftGoals}-{rightGoals}  {AwayTeamRank}. {AwayTeam.TeamName}\t\t\t\tTime:{DisplayTime}";
    }

    public override void _PhysicsProcess(double delta)
    {
        TimeLeft -= (float)delta;
        for (int i = 0; i < TeamSize; i++)
        {
            if (HomeTeam.Players[i].ShouldReact())
            {
                var actions = AgentObserve(i);
                AgentAct(i, actions);
            }
        }

        for (int i = 0; i < TeamSize; i++)
        {
            if (AwayTeam.Players[i].ShouldReact())
            {
                var actions = OppAgentObserve(i);
                OppAgentAct(i, actions);
            }
        }

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
        float margin = 1f;

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
        SetInitialMatchPos();
    }

    private void SetInitialMatchPos()
    {
        var start = leftGoals > rightGoals ? Vector2.Right : Vector2.Left;
        SetPosition(Ball, Vector2.Zero + WorldParameters.Meters(start * 5));

        for (int i = 0; i < TeamSize; i++)
        {
            var player = HomeTeamPlayers[i].Body;
            var opp = AwayTeamPlayers[i].Body;
            float xOffset = (WorldParameters.Meters(Width) / 2) * 0.3f;
            float yOffset = (WorldParameters.Meters(Height / 3) / (TeamSize + 1)) * (i + 1) - (WorldParameters.Meters(Height / 3) / 2);
            SetPosition(player, new Vector2(-xOffset, yOffset) + WorldParameters.RandomVectorMeters(0));
            SetPosition(opp, new Vector2(xOffset, -yOffset) + WorldParameters.RandomVectorMeters(0));
            player.Rotation = Mathf.Pi / 2f;
            opp.Rotation = -Mathf.Pi / 2f;
        }
    }

    private void OnLeftGoal(Node2D body)
    {
        if (body != Ball)
            return;

        rightGoals++;

        var player = BallScript.LastTouchedPlayer;
        var prevPlayer = BallScript.PreviousTouchedPlayer;

        if (player != null)
        {
            if (player.OwnerClub == AwayTeam)
                player.GoalsScored++;
            else
                player.OwnGoals++;
        }

        if(prevPlayer!=null && prevPlayer != player)
        {
            if (prevPlayer.OwnerClub == AwayTeam)
                prevPlayer.Assists++;
        }

        ResetPositions();
    }

    private void OnRightGoal(Node2D body)
    {
        if (body != Ball)
            return;

        leftGoals++;

        var player = BallScript.LastTouchedPlayer;
        var prevPlayer = BallScript.PreviousTouchedPlayer;

        if (player != null)
        {
            if (player.OwnerClub == HomeTeam)
                player.GoalsScored++;
            else
                player.OwnGoals++;
        }

        if(prevPlayer!=null && prevPlayer != player)
        {
            if (prevPlayer.OwnerClub == HomeTeam)
                prevPlayer.Assists++;
        }

        ResetPositions();
    }

    private float[] AgentObserve(int ind)
    {
        var inputs = new List<float>();
        var player = HomeTeamPlayers[ind].Body;

        float maxDistance = new Vector2(Width, Height).Length();

        // Ball (local)
        var playerToBallVec = WorldParameters.ToMeters(Ball.Position - player.Position).Rotated(-player.Rotation);
        var dirToBall = playerToBallVec.Normalized();

        inputs.Add(dirToBall.X);
        inputs.Add(dirToBall.Y);
        inputs.Add(playerToBallVec.Length() / maxDistance);
        inputs.Add(1 - (Mathf.Clamp(playerToBallVec.Length(), 0, 5) / 5));

        // Ball velocity (local)
        var ballVel = WorldParameters.ToMeters(Ball.LinearVelocity).Rotated(-player.Rotation) / WorldParameters.BallSpeed;
        inputs.Add(ballVel.X);
        inputs.Add(ballVel.Y);

        // Player velocity (local)
        var playerVel = WorldParameters.ToMeters(player.LinearVelocity).Rotated(-player.Rotation) / WorldParameters.PlayerSpeed;
        inputs.Add(playerVel.X);
        inputs.Add(playerVel.Y);

        // Player angular velocity
        inputs.Add(player.AngularVelocity / ((Player)player).MaxAngularVelocity);

        // Player stamina
        inputs.Add(((Player)player).CurrentStamina);

        // Left goal (local)
        var playerToLeftGoalVec = WorldParameters.ToMeters(LeftGoal.Position - player.Position).Rotated(-player.Rotation);
        var dirToLeftGoal = playerToLeftGoalVec.Normalized();

        inputs.Add(dirToLeftGoal.X);
        inputs.Add(dirToLeftGoal.Y);
        inputs.Add(playerToLeftGoalVec.Length() / maxDistance);

        // Right goal (local)
        var playerToRightGoalVec = WorldParameters.ToMeters(RightGoal.Position - player.Position).Rotated(-player.Rotation);
        var dirToRightGoal = playerToRightGoalVec.Normalized();

        inputs.Add(dirToRightGoal.X);
        inputs.Add(dirToRightGoal.Y);
        inputs.Add(playerToRightGoalVec.Length() / maxDistance);

        // Match state
        inputs.Add((leftGoals - rightGoals) / 5f);
        inputs.Add(1 - (TimeLeft / MatchTime));

        // Opponents (local)
        for (int i = 0; i < TeamSize; i++)
        {
            var opp = AwayTeamPlayers[i];

            var playerToOppVec = WorldParameters.ToMeters(opp.Body.Position - player.Position).Rotated(-player.Rotation);
            var dirToOpp = playerToOppVec.Normalized();

            inputs.Add(dirToOpp.X);
            inputs.Add(dirToOpp.Y);
            inputs.Add(playerToOppVec.Length() / maxDistance);
            inputs.Add(1 - (Mathf.Clamp(playerToOppVec.Length(), 0, 5) / 5));

            var oppVel = WorldParameters.ToMeters(opp.Body.LinearVelocity).Rotated(-player.Rotation) / WorldParameters.PlayerSpeed;
            inputs.Add(oppVel.X);
            inputs.Add(oppVel.Y);
        }

        // Teammates (local)
        for (int i = 0; i < TeamSize; i++)
        {
            if (i == ind)
                continue;

            var teammate = HomeTeamPlayers[i];

            var playerToTeammateVec = WorldParameters.ToMeters(teammate.Body.Position - player.Position).Rotated(-player.Rotation);
            var dirToTeammate = playerToTeammateVec.Normalized();

            inputs.Add(dirToTeammate.X);
            inputs.Add(dirToTeammate.Y);
            inputs.Add(playerToTeammateVec.Length() / maxDistance);
            inputs.Add(1 - (Mathf.Clamp(playerToTeammateVec.Length(), 0, 5) / 5));

            var teammateVel = WorldParameters.ToMeters(teammate.Body.LinearVelocity).Rotated(-player.Rotation) / WorldParameters.PlayerSpeed;
            inputs.Add(teammateVel.X);
            inputs.Add(teammateVel.Y);
        }

        foreach (var memory in HomeTeamPlayers[ind].Memory)
            inputs.Add(memory);

        return HomeTeam.Players[ind].Brain.forward(inputs.ToArray());
    }

    private void AgentAct(int ind, float[] actions)
    {
        var playerScript = HomeTeamPlayers[ind].Script;

        playerScript.Move.X = actions[0];
        playerScript.Move.Y = actions[1];
        playerScript.RotateBody = actions[2];
        playerScript.KickDir.X = actions[3];
        playerScript.KickDir.Y = actions[4];
        playerScript.KickPower = actions[5];
        playerScript.BallSpin = actions[6];

        for (int i = 0; i < HomeTeamPlayers[ind].Memory.Length; i++)
        {
            HomeTeamPlayers[ind].Memory[i] = actions[6 + i];
        }
    }

    private float[] OppAgentObserve(int ind)
    {
        var inputs = new List<float>();
        var opponent = AwayTeamPlayers[ind].Body;

        float maxDistance = new Vector2(Width, Height).Length();

        // Ball (local)
        var opponentToBallVec = WorldParameters.ToMeters(Ball.Position - opponent.Position).Rotated(-opponent.Rotation);
        var dirToBall = opponentToBallVec.Normalized();

        inputs.Add(dirToBall.X);
        inputs.Add(dirToBall.Y);
        inputs.Add(opponentToBallVec.Length() / maxDistance);
        inputs.Add(1 - (Mathf.Clamp(opponentToBallVec.Length(), 0, 5) / 5));

        // Ball velocity
        var ballVel = WorldParameters.ToMeters(Ball.LinearVelocity).Rotated(-opponent.Rotation) / WorldParameters.BallSpeed;
        inputs.Add(ballVel.X);
        inputs.Add(ballVel.Y);

        // Own velocity
        var opponentVel = WorldParameters.ToMeters(opponent.LinearVelocity).Rotated(-opponent.Rotation) / WorldParameters.PlayerSpeed;
        inputs.Add(opponentVel.X);
        inputs.Add(opponentVel.Y);

        // Player angular velocity
        inputs.Add(opponent.AngularVelocity / ((Player)opponent).MaxAngularVelocity);

        // Stamina
        inputs.Add(((Player)opponent).CurrentStamina);

        // Own goal (Right)
        var opponentToOwnGoalVec = WorldParameters.ToMeters(RightGoal.Position - opponent.Position).Rotated(-opponent.Rotation);
        var dirToOwnGoal = opponentToOwnGoalVec.Normalized();

        inputs.Add(dirToOwnGoal.X);
        inputs.Add(dirToOwnGoal.Y);
        inputs.Add(opponentToOwnGoalVec.Length() / maxDistance);

        // Enemy goal (Left)
        var opponentToEnemyGoalVec = WorldParameters.ToMeters(LeftGoal.Position - opponent.Position).Rotated(-opponent.Rotation);
        var dirToEnemyGoal = opponentToEnemyGoalVec.Normalized();

        inputs.Add(dirToEnemyGoal.X);
        inputs.Add(dirToEnemyGoal.Y);
        inputs.Add(opponentToEnemyGoalVec.Length() / maxDistance);

        // Match state
        inputs.Add((rightGoals - leftGoals) / 5f);
        inputs.Add(1 - (TimeLeft / MatchTime));

        // Opponents
        for (int i = 0; i < TeamSize; i++)
        {
            var opp = HomeTeamPlayers[i];

            var toOppVec = WorldParameters.ToMeters(opp.Body.Position - opponent.Position).Rotated(-opponent.Rotation);
            var dirToOpp = toOppVec.Normalized();

            inputs.Add(dirToOpp.X);
            inputs.Add(dirToOpp.Y);
            inputs.Add(toOppVec.Length() / maxDistance);
            inputs.Add(1 - (Mathf.Clamp(toOppVec.Length(), 0, 5) / 5));

            var oppVel = WorldParameters.ToMeters(opp.Body.LinearVelocity).Rotated(-opponent.Rotation) / WorldParameters.PlayerSpeed;

            inputs.Add(oppVel.X);
            inputs.Add(oppVel.Y);
        }

        // Teammates
        for (int i = 0; i < TeamSize; i++)
        {
            if (i == ind)
                continue;

            var teammate = AwayTeamPlayers[i];

            var toTeammateVec = WorldParameters.ToMeters(teammate.Body.Position - opponent.Position).Rotated(-opponent.Rotation);
            var dirToTeammate = toTeammateVec.Normalized();

            inputs.Add(dirToTeammate.X);
            inputs.Add(dirToTeammate.Y);
            inputs.Add(toTeammateVec.Length() / maxDistance);
            inputs.Add(1 - (Mathf.Clamp(toTeammateVec.Length(), 0, 5) / 5));

            var teammateVel = WorldParameters.ToMeters(teammate.Body.LinearVelocity).Rotated(-opponent.Rotation) / WorldParameters.PlayerSpeed;

            inputs.Add(teammateVel.X);
            inputs.Add(teammateVel.Y);
        }

        foreach (var memory in AwayTeamPlayers[ind].Memory)
            inputs.Add(memory);

        return AwayTeam.Players[ind].Brain.forward(inputs.ToArray());
    }

    private void OppAgentAct(int ind, float[] actions)
    {
        var opponentScript = AwayTeamPlayers[ind].Script;

        opponentScript.Move.X = actions[0];
        opponentScript.Move.Y = actions[1];
        opponentScript.RotateBody = actions[2];
        opponentScript.KickDir.X = actions[3];
        opponentScript.KickDir.Y = actions[4];
        opponentScript.KickPower = actions[5];
        opponentScript.BallSpin = actions[6];

        for (int i = 0; i < AwayTeamPlayers[ind].Memory.Length; i++)
        {
            AwayTeamPlayers[ind].Memory[i] = actions[6 + i];
        }
    }

    public class AIPlayer
    {
        public RigidBody2D Body;
        public Player Script;
        public bool IsAwayTeam;
        public bool Active;

        public float[] Memory = new float[TrainingParameters.MemorySize];
    }
}