using Godot;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

public partial class TrainerStage3 : Node2D
{
    [Export] public float MutationChance = 6f;
    [Export] public float MutationAmount = 0.3f;
    public int[] ShapeNN = TrainingParameters.NNShapeS2.ToArray();

    [Export] public int PopulationSize = 100;
    [Export] public int Elites = 10;
    private List<AgentStage3> Population = new();
    private List<Node2D> Fields = new();

    [Export] public float FieldWidth = 90f;
    [Export] public float FieldHeight = 45f;
    [Export] public float GoalHeight = 10f;
    [Export] public float BoundsOffset = 5f;

    [Export] public int NumOpponents = 3;
    [Export] public int currActiveOpponents = 2;

    [Export] public PackedScene FieldBuilderStage3;

    [Export] public float GenTime = 30f;

    [Export] public int GensWithoutImprovement = 20;
    private int GensWithoutImprovementCounter = 0;
    private AgentStage3 bestAgent;

    [Export] public string preTrainedAIPath;

    [Export] public float TimeScale = 1;

    private int Gen = 0;
    private float GenTimer;

    public bool TrainingComplete = false;

    public override void _Ready()
    {
        if (TrainingParameters.isSim)
        {
            NumOpponents = TrainingParameters.TeamSize;
        }

        ShapeNN[0] += NumOpponents * 6;

        CreatePopulation();
        SpawnEnvironments();
    }

    public override void _PhysicsProcess(double delta)
    {
        Engine.TimeScale = TimeScale;
        Engine.PhysicsTicksPerSecond = 60 * (int)TimeScale;

        GenTimer += (float)delta;
        if (GenTimer >= GenTime)
        {
            LogResults();

            Gen += 1;
            GenTimer = 0;

            CreateNextGen();
            SpawnEnvironments();
        }
    }

    private void CreatePopulation()
    {
        NN baseNN;
        if (!string.IsNullOrEmpty(preTrainedAIPath))
        {
            baseNN = NN.LoadSavedNN(preTrainedAIPath);
            baseNN.MutationAmt = MutationAmount;
            baseNN.MutationChance = MutationChance;
        }
        else
        {
            baseNN = NN.LoadSavedNN("C:/Projects/Godot/Proects/football-sim/AI/Training Stage 2/brain.txt");
            baseNN = NN.Extend(baseNN, ShapeNN);
            baseNN.MutationAmt = MutationAmount;
            baseNN.MutationChance = MutationChance;
        }

        Population.Clear();

        for (int i = 0; i < PopulationSize; i++)
        {
            Population.Add(new AgentStage3(baseNN.clone()));
        }
        bestAgent = Population[0];
    }

    private void SpawnEnvironments()
    {
        foreach (var field in Fields)
            field.QueueFree();

        Fields.Clear();

        int cols = Mathf.CeilToInt(Mathf.Sqrt(PopulationSize));

        for (int i = 0; i < Population.Count; i++)
        {
            var field = FieldBuilderStage3.Instantiate<Node2D>();

            int row = i / cols;
            int col = i % cols;

            field.Position = WorldParameters.Meters(new Vector2(
                col * (FieldWidth + BoundsOffset * 2 + 4),
                row * (FieldHeight + BoundsOffset * 2 + 4)
            ));

            ((FieldBuilderStage3)field).Agent = Population[i];
            ((FieldBuilderStage3)field).Height = FieldHeight;
            ((FieldBuilderStage3)field).Width = FieldWidth;
            ((FieldBuilderStage3)field).GoalHeight = GoalHeight;
            ((FieldBuilderStage3)field).BoundsOffset = BoundsOffset;
            ((FieldBuilderStage3)field).NumOpponents = NumOpponents;
            ((FieldBuilderStage3)field).currActiveOpponents = currActiveOpponents;

            AddChild(field);

            Fields.Add(field);
        }
    }

    private void CreateNextGen()
    {
        Population.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));

        if (bestAgent.Fitness < Population[0].Fitness)
        {
            bestAgent = Population[0].Clone();
            bestAgent.Fitness = Population[0].Fitness;
            bestAgent.Brain.save("C:/Projects/Godot/Proects/football-sim/AI/Training Stage 3/brain.txt");
            GensWithoutImprovementCounter = 0;
        }
        else
        {
            GensWithoutImprovementCounter++;
            
            if (GensWithoutImprovementCounter > GensWithoutImprovement)
            {
                if (currActiveOpponents < NumOpponents)
                {
                    GensWithoutImprovementCounter = 0;
                    bestAgent.Fitness = 0;
                    currActiveOpponents++;
                }
                else
                {
                    TrainingComplete = true;
                }
            }
        }

        for (int i = 0; i < PopulationSize - Elites; i++)
        {
            var parent = TournamentScelect();
            var child = new AgentStage3(parent.Brain.copyWithMutation());

            Population.Add(child);
        }

        for (int i = 0; i < Elites; i++)
        {
            Population[i].Fitness = 0;
        }

        for (int i = 0; i < PopulationSize - Elites; i++)
        {
            Population.RemoveAt(Elites);
        }
    }

    private AgentStage3 TournamentScelect()
    {
        var ind = PopulationSize - 1;

        for (int i = 0; i < 3; i++)
        {
            var newInd = GD.RandRange(0, PopulationSize - 1);

            if (newInd < ind)
            {
                ind = newInd;
            }
        }

        return Population[ind];
    }

    private void LogResults()
    {
        var sum = 0f;

        foreach (var agent in Population)
        {
            sum += agent.Fitness;
        }

        GD.Print($"Average Fitness Gen {Gen} - {sum / PopulationSize}");
    }
}