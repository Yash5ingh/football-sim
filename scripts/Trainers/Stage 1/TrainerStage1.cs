using Godot;
using System.Collections.Generic;
using System.Diagnostics;

public partial class TrainerStage1 : Node2D
{
    [Export] public float MutationChance = 6f;
    [Export] public float MutationAmount = 0.3f;
    public int[] ShapeNN = TrainingParameters.NNShapeS1;

    [Export] public int PopulationSize = 100;
    [Export] public int Elites = 10;
    private List<AgentStage1> Population = new();
    private List<Node2D> Fields = new();

    [Export] public float FieldWidth = 90f;
    [Export] public float FieldHeight = 45f;
    [Export] public float BoundsOffset = 5f;

    [Export] public PackedScene FieldBuilderStage1;

    [Export] public float GenTime = 30f;

    [Export] public int GensWithoutImprovement = 20;
    private int GensWithoutImprovementCounter = 0;
    private AgentStage1 bestAgent;
    private float AverageFitness;
    private float BestAverageFitness;
    private int Gen = 0;
    private float GenTimer;

    public bool TrainingComplete = false;
    private Stopwatch sw = Stopwatch.StartNew();

    public override void _Ready()
    {
        CreatePopulation();
        SpawnEnvironments();
    }

    public override void _PhysicsProcess(double delta)
    {
        GenTimer += (float)delta;
        if(GenTimer >= GenTime)
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
        Population.Clear();

        for (int i = 0; i < PopulationSize; i++)
        {
            Population.Add(new AgentStage1(new NN(ShapeNN, MutationAmount, MutationChance)));
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
            var field = FieldBuilderStage1.Instantiate<Node2D>();

            int row = i / cols;
            int col = i % cols;

            field.Position = WorldParameters.Meters(new Vector2(
                col * (FieldWidth + BoundsOffset*2 + 4),
                row * (FieldHeight + BoundsOffset*2 + 4)
            ));

            ((FieldBuilderStage1)field).Agent = Population[i];
            ((FieldBuilderStage1)field).Height = FieldHeight;
            ((FieldBuilderStage1)field).Width = FieldWidth;
            ((FieldBuilderStage1)field).BoundsOffset = BoundsOffset;

            AddChild(field);

            Fields.Add(field);
        }
    }

    private void CreateNextGen()
    {
        Population.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));

        if(BestAverageFitness < AverageFitness)
        {
            bestAgent = Population[0].Clone();
            bestAgent.Fitness = Population[0].Fitness;
            BestAverageFitness = AverageFitness;
            bestAgent.Brain.save("C:/Projects/Godot/Proects/football-sim/AI/Training Stage 1/brain.txt");
            GensWithoutImprovementCounter = 0;

            GD.Print("New Best Gen");
        }
        else
        {
            GensWithoutImprovementCounter++;
            if(GensWithoutImprovementCounter > GensWithoutImprovement)
            {
                TrainingComplete = true;
            }
        }

        for(int i=0; i < PopulationSize - Elites; i++)
        {
            var parent = TournamentScelect();
            var child = new AgentStage1(parent.Brain.copyWithMutation());

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

    private AgentStage1 TournamentScelect()
    {
        var ind = PopulationSize-1;

        for(int i = 0; i < 3; i++)
        {
            var newInd = GD.RandRange(0, PopulationSize-1);

            if(newInd < ind)
            {
                ind = newInd;
            }
        }

        return Population[ind];
    }

    private void LogResults()
    {
        var sum = 0f;
        var avgMutationAmt = 0f;
        var avgMutationChance = 0f;
        foreach (var agent in Population)
        {
            sum += agent.Fitness;
            avgMutationAmt += agent.Brain.MutationAmt;
            avgMutationChance += agent.Brain.MutationChance;
        }

        AverageFitness = sum / PopulationSize;
        GD.Print($"Average Fitness Gen {Gen} : {AverageFitness} \t---\t MA : {avgMutationAmt / PopulationSize:F4} - MC : {avgMutationChance / PopulationSize:F4} - T : {sw.Elapsed.TotalSeconds / (Gen + 1):F2}s");
    }
}