using Godot;
using System;

public partial class AgentStage3
{
	public NN Brain;
	public float Fitness;

	public AgentStage3(NN brain)
	{
		Brain = brain;
    }

    public AgentStage3 Clone()
    {
        return new AgentStage3(Brain.clone());
    }
}
