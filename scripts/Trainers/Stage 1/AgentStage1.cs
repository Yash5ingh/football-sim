using Godot;
using System;

public partial class AgentStage1
{
	public NN Brain;
	public float Fitness;

	public AgentStage1(NN brain)
	{
		Brain = brain;
		Fitness = 0;
    }

	public AgentStage1 Clone()
	{
		return new AgentStage1(Brain.clone());
	}
}
