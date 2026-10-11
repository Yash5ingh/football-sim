using Godot;
using System;
using static AgentStage6;

public class ClubPlayer
{
	public Guid Id;
	public string Name;
    public int Age;
    public NN Brain;

    public int NumStats = 4;
    public float PlayerSpeed;
    public float playerStrength;
    public float playerStamina;
    public float reactionSpeed;

    public int Number;
    public Club OwnerClub;

    public int GoalsScored;
    public int Assists;
    public int Touches;
    public int Passes;
    public int Steals;
    public int OwnGoals;
    public int BallLost;
    public float PassDistance;
    public float DribbleDistance;

    public int Wins;
    public int Losses;
    public int Draws;


    public float PlayerScore => GetPlayerScore();

    private int reactionCounter;
    private int reactionFrames;

    public ClubPlayer()
    {
        Age = 20;
        InitReaction();
    }

    public ClubPlayer(PlayerAI basePlayer)
    {
        Id = Guid.NewGuid();
        
        Name = NameGenerator.RandomPlayerName();
        Age = 20;
        Brain = basePlayer.Brain.clone();
        PlayerSpeed = basePlayer.PlayerSpeed;
        playerStrength = basePlayer.playerStrength;
        playerStamina = basePlayer.playerStamina;
        reactionSpeed = basePlayer.reactionSpeed;

        InitReaction();
    }

    public static ClubPlayer CreateMutatedClone(ClubPlayer parent)
    {
        var player = new ClubPlayer();

        player.Id = Guid.NewGuid();
        player.Name = NameGenerator.RandomPlayerName();
        player.Brain = parent.Brain.copyWithMutation();
        player.PlayerSpeed = parent.PlayerSpeed;
        player.playerStrength = parent.playerStrength;
        player.playerStamina = parent.playerStamina;
        player.reactionSpeed = parent.reactionSpeed;
        player.MutatePlayerStats();
        player.InitReaction();

        return player;
    }

    public void MutatePlayerStats(float mutationChance = 0.2f, float mutationAmount = 0.05f)
    {
        if (GD.Randf() >= mutationChance)
            return;

        float delta = (float)GD.RandRange(-mutationAmount, mutationAmount);

        int gain = (int)(GD.Randi() % NumStats);
        int lose;

        do
        {
            lose = (int)(GD.Randi() % NumStats);
        }
        while (lose == gain);

        float[] stats =
        {
                PlayerSpeed,
                playerStrength,
                reactionSpeed,
                playerStamina
            };


        float gainMin = 0f - stats[gain];
        float gainMax = 1f - stats[gain];

        float loseMin = stats[lose] - 1f;
        float loseMax = stats[lose] - 0f;

        float lower = Mathf.Max(gainMin, loseMin);
        float upper = Mathf.Min(gainMax, loseMax);

        if (lower > upper)
        {
            return;
        }

        delta = Mathf.Clamp(delta, lower, upper);

        stats[gain] += delta;
        stats[lose] -= delta;

        PlayerSpeed = stats[0];
        playerStrength = stats[1];
        reactionSpeed = stats[2];
        playerStamina = stats[3];
        InitReaction();
    }

    private void InitReaction()
    {
        reactionFrames = 23 - Mathf.RoundToInt(reactionSpeed * 20);
        reactionCounter = GD.RandRange(0, reactionFrames - 1);
    }

    public bool ShouldReact()
    {
        reactionCounter++;

        if (reactionCounter >= reactionFrames)
        {
            reactionCounter = 0;
            return true;
        }

        return false;
    }

    public void ResetSeasonStats()
    {
        GoalsScored = 0;
        Assists = 0;
        Touches = 0;
        Passes = 0;
        OwnGoals = 0;
        DribbleDistance = 0;
        PassDistance = 0;
        BallLost = 0;
        Steals = 0;
        Wins = 0;
        Losses = 0;
        Draws = 0;
    }

    private float GetPlayerScore()
    {
        var positiveStats = GoalsScored * 30 + Assists * 20 + Steals * 2;
        var distanceStats = PassDistance * .1f + DribbleDistance * 0.15f;
        var negativeStats = OwnGoals * 10 + BallLost * 3;
        var teamStats = Wins * 40 + Draws * 10 - Losses * 15;
        return positiveStats - negativeStats + teamStats + distanceStats;
    }

    public void Save(string folder)
    {
        DirAccess.MakeDirRecursiveAbsolute(folder);

        Brain.save($"{folder}/Brain.txt");

        using var profile = FileAccess.Open(
            $"{folder}/Profile.txt",
            FileAccess.ModeFlags.Write);

        profile.StoreLine($"ID={Id}");
        profile.StoreLine($"NAME={Name}");
        profile.StoreLine($"AGE={Age}");

        using var physical = FileAccess.Open(
            $"{folder}/PhysicalAttributes.txt",
            FileAccess.ModeFlags.Write);

        physical.StoreLine($"SPEED={PlayerSpeed}");
        physical.StoreLine($"STRENGTH={playerStrength}");
        physical.StoreLine($"REACTION={reactionSpeed}");
        physical.StoreLine($"STAMINA={playerStamina}");
    }

    public static ClubPlayer Load(string folder, float mutationChance, float mutationAmt)
    {
        Guid id = Guid.Empty;
        string name = "";
        int age = 20;
        using (var profile = FileAccess.Open(
            $"{folder}/Profile.txt",
            FileAccess.ModeFlags.Read))
        {
            while (!profile.EofReached())
            {
                var line = profile.GetLine();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var split = line.Split('=');

                if (split.Length != 2)
                    continue;

                switch (split[0])
                {
                    case "ID":
                        id = Guid.Parse(split[1]);
                        break;

                    case "NAME":
                        name = split[1];
                        break;

                    case "AGE":
                        age = int.Parse(split[1]);
                        break;
                }
            }
        }

        NN brain = NN.LoadSavedNN($"{folder}/Brain.txt");
        brain.MutationChance = mutationChance;
        brain.MutationAmt = mutationAmt;

        float speed = .75f;
        float strength = .75f;
        float reaction = .75f;
        float stamina = .75f;

        using (var physical = FileAccess.Open(
            $"{folder}/PhysicalAttributes.txt",
            FileAccess.ModeFlags.Read))
        {
            while (!physical.EofReached())
            {
                var line = physical.GetLine();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var split = line.Split('=');

                if (split.Length != 2)
                    continue;

                switch (split[0])
                {
                    case "SPEED":
                        speed = float.Parse(split[1]);
                        break;

                    case "STRENGTH":
                        strength = float.Parse(split[1]);
                        break;

                    case "REACTION":
                        reaction = float.Parse(split[1]);
                        break;

                    case "STAMINA":
                        stamina = float.Parse(split[1]);
                        break;
                }
            }
        }

        ClubPlayer player = new ClubPlayer();

        player.Id = id;
        player.Name = name;
        player.Brain = brain;
        player.PlayerSpeed = speed;
        player.playerStrength = strength;
        player.reactionSpeed = reaction;
        player.playerStamina = stamina;
        player.Age = age;
        player.InitReaction();

        return player;
    }
}
