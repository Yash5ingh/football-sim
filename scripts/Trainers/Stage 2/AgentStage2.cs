using Godot;
using System;

public partial class AgentStage2
{
	public NN Brain;
	public float Fitness;

	public AgentStage2(NN brain)
	{
		Brain = brain;
    }

    public AgentStage2 Clone()
    {
        return new AgentStage2(Brain.clone());
    }
}
