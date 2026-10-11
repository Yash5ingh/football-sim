using Godot;
using System;

public partial class AgentStage4
{
    public NN Brain;
    public float Fitness;

    public AgentStage4(NN brain)
    {
        Brain = brain;
        Fitness = 0;
    }

    public AgentStage4 Clone()
    {
        return new AgentStage4(Brain.clone());
    }
}
