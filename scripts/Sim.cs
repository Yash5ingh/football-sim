using Godot;

public partial class Sim : Node2D
{
    [Export] public PackedScene TrainerStage1;
    [Export] public PackedScene TrainerStage2;
    [Export] public PackedScene TrainerStage3;
    [Export] public PackedScene TrainerStage4;
    [Export] public PackedScene TrainerStage5;
    [Export] public PackedScene TrainerStage6;

    [Export] public int StartingStage = 1;

    private Node _currentTrainer;
    private int _stage = 1;

    public override void _Ready()
    {
        _stage = StartingStage;
        TrainingParameters.isSim = true;
        SwitchStage();
    }
  
    public override void _PhysicsProcess(double delta)
    {
        if (_currentTrainer == null)
            return;

        dynamic trainer = _currentTrainer;

        if (trainer.TrainingComplete)
        {
            _currentTrainer.QueueFree();

            _stage++;
            SwitchStage();
        }
    }

    private void SwitchStage()
    {
        switch (_stage)
        {
            case 1:
                StartStage4();
                break;

            case 2:
                StartStage5();
                break;

            case 3:
                StartStage6();
                break;

            default:
                GD.Print("Training complete!");
                break;
        }
    }

    private void StartStage1()
    {
        _currentTrainer = TrainerStage1.Instantiate();
        AddChild(_currentTrainer);
    }

    private void StartStage2()
    {
        _currentTrainer = TrainerStage2.Instantiate();
        AddChild(_currentTrainer);
    }

    private void StartStage3()
    {
        _currentTrainer = TrainerStage3.Instantiate();
        AddChild(_currentTrainer);
    }

    private void StartStage4()
    {
        _currentTrainer = TrainerStage4.Instantiate();
        AddChild(_currentTrainer);
    }

    private void StartStage5()
    {
        _currentTrainer = TrainerStage5.Instantiate();
        AddChild(_currentTrainer);
    }

    private void StartStage6()
    {
        _currentTrainer = TrainerStage6.Instantiate();
        AddChild(_currentTrainer);
    }
}