using Godot;
using static AgentStage6;
using static System.Runtime.InteropServices.JavaScript.JSType;

public partial class Player : RigidBody2D
{
    // Designer-friendly values
    [Export] public float TopSpeed = 10f; // m/s
    [Export] public float MaxAngularVelocity = 20f; // rad/s

    public float KickCooldown = 0.2f;

    public float PlayerSpeed = 1;
    public float playerStrength = 1;
    public float playerStamina = 1;

    private float _currStamina;
    public float CurrentStamina => Mathf.Clamp(_currStamina / playerStamina, 0f, 1f);

    private float _moveForce;
    private float _rotateForce;

    public RigidBody2D _ball;
    private double _nextKickTime = 0;

    public bool isAi = false;

    public Vector2 KickDir, Move;
    public float KickPower, RotateBody, BallSpin;

    private float actualKickPower;

    private RichTextLabel PlayerNumberText;
    private RichTextLabel PlayerDisplay;
    private int PlayerNumber;
    private string PlayerName;

    public bool isAwayTeam;

    private ClubPlayer Agent;

    private bool _displayName;
    private bool _displayStamina;
    private bool _staminaBorder;

    private Color ColorPrimary, ColorSecondary;

    [Export]
    public bool DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value)
                return;

            if (PlayerDisplay == null)
                return;

            _displayName = value;
            RefreshDisplay();
        }
    }

    [Export]
    public bool DisplayStamina
    {
        get => _displayStamina;
        set
        {
            if (_displayStamina == value)
                return;

            if (PlayerDisplay == null)
                return;

                _displayStamina = value;
            RefreshDisplay();
        }
    }

    [Export]
    public bool StaminaBorder
    {
        get => _staminaBorder;
        set
        {
            if (_staminaBorder == value)
                return;

            if (PlayerDisplay == null)
                return;

            _staminaBorder = value;
            RefreshDisplay();
        }
    }

    [Export] public bool DisplayKick = false;

    public override void _Ready()
    {
        var kickArea = GetNode<Area2D>("KickArea");
        kickArea.BodyEntered += OnBodyEntered;
        kickArea.BodyExited += OnBodyExited;

        PlayerDisplay = GetNode<RichTextLabel>("PlayerDisplay");

        KickDir = Vector2.Zero;
        Move = Vector2.Zero;

        // Calculate the force needed to achieve the desired top speed.
        // F = m * damping * v
        CalculateInitialParameters();
    }

    public override void _Process(double delta)
    {
        if (DisplayStamina)
            UpdateDisplayText();

        if (StaminaBorder)
            SetBorderStaminaColor(CurrentStamina);
    }

    private void RefreshDisplay()
    {
        UpdateDisplayText();

        if (StaminaBorder)
            SetBorderStaminaColor(CurrentStamina);
        else
            SetBorderColor(ColorSecondary);
    }

    private void UpdateDisplayText()
    {
        string text;

        if (DisplayName && DisplayStamina)
            text = $"{PlayerName} {CurrentStamina * 100:F0}";
        else if (DisplayName)
            text = PlayerName;
        else if (DisplayStamina)
            text = $"{CurrentStamina * 100:F0}";
        else
            text = "";

        if (PlayerDisplay.Text != text)
            PlayerDisplay.Text = text;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (DisplayKick)
        {
            if (KickPower > 0)
                SetBorderColor(ColorPrimary);
            else
                SetBorderColor(ColorSecondary);
        }

        float effort = Mathf.Clamp(
            Move.Length() + Mathf.Abs(RotateBody) * 0.25f,
            0f,
            1f
        );

        if (_currStamina < playerStamina)
        {
            _currStamina += (.5f-(effort*effort)) * (float)delta * 0.02f;
            _currStamina = Mathf.Min(playerStamina, _currStamina);
        }

        actualKickPower = KickPower * WorldParameters.BallSpeed * 3f;

        Vector2 forward = Vector2.Up.Rotated(Rotation);
        Vector2 right = Vector2.Right.Rotated(Rotation);

        if (Move != Vector2.Zero || RotateBody != 0)
        {
            _currStamina -= (effort*effort) * (float)delta * 0.01f;
            _currStamina = Mathf.Max(playerStamina * 0.4f, _currStamina);

            float forwardMove = Move.X;
            float sideMove = Move.Y;

            // Backpedal slower
            if (forwardMove < 0)
                forwardMove *= 0.35f;

            // Shuffle slower than sprinting
            sideMove *= 0.4f;

            forwardMove *= CurrentStamina;
            sideMove *= CurrentStamina;

            Vector2 movement =
                forward * forwardMove +
                right * sideMove;

            var kickPenalty = KickPower > 0 ? .8f : 1f;

            ApplyCentralForce(
                movement * _moveForce * WorldParameters.UnitsPerMeter * kickPenalty
            );

            float actualRotate = RotateBody * CurrentStamina;

            ApplyTorque(
                actualRotate * _rotateForce * WorldParameters.UnitsPerMeter * kickPenalty
            );
        }

        if (_ball == null)
            return;

        if (Time.GetTicksMsec() < _nextKickTime)
            return;

        if (KickDir == Vector2.Zero)
            return;

        KickDir = KickDir.Normalized();
        
        if (KickPower > 0)
        {   
            _currStamina -= (KickPower / 200f);
            _currStamina = Mathf.Max(playerStamina*.4f, _currStamina);

            Vector2 forwardKick = KickDir.Rotated(Rotation);
            float alignment = forward.Dot(forwardKick);
            float kickEfficiency = Mathf.Lerp(0.2f, 1f, (alignment + 1f) * 0.5f);

            _ball.LinearVelocity *= .2f;
            _ball.ApplyCentralImpulse(
                forwardKick * WorldParameters.UnitsPerMeter *
                actualKickPower *
                kickEfficiency *
                playerStrength *
                CurrentStamina
            );

            ((Ball)_ball).Spin = BallSpin;

            if(Agent != null)
            {
                var ballScript = ((Ball)_ball);
                if (ballScript.LastTouchedPlayer != null)
                {
                    if(ballScript.LastTouchedPlayer.OwnerClub == Agent.OwnerClub)
                    {
                        if(ballScript.LastTouchedPlayer != Agent)
                        {
                            ballScript.LastTouchedPlayer.Passes++;
                            ballScript.LastTouchedPlayer.PassDistance += WorldParameters.ToMeters((_ball.Position - ballScript.LastTouchPosition).Length());
                        }
                        else
                        {
                            Agent.DribbleDistance += WorldParameters.ToMeters((_ball.Position - ballScript.LastTouchPosition).Length());
                        }
                    }
                    else
                    {
                        ballScript.LastTouchedPlayer.BallLost++;
                        Agent.Steals++;
                    }
                    ballScript.PreviousTouchedPlayer = ballScript.LastTouchedPlayer;
                }
                ballScript.LastTouchedPlayer = Agent;
                ballScript.LastTouchPosition = _ball.Position;
                ballScript.HomePossession = !isAwayTeam;
                Agent.Touches++;
            }

            float maxBallSpeed = WorldParameters.UnitsPerMeter * WorldParameters.BallSpeed;
            if (_ball.LinearVelocity.LengthSquared() > maxBallSpeed * maxBallSpeed)
            {
                _ball.LinearVelocity =
                    _ball.LinearVelocity.Normalized() * maxBallSpeed;
            }

            _nextKickTime = Time.GetTicksMsec() + KickCooldown * 1000.0;
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body is RigidBody2D rb)
            _ball = rb;
    }

    private void OnBodyExited(Node body)
    {
        if (body == _ball)
            _ball = null;
    }

    public void Stop()
    {
        Move = Vector2.Zero;
        KickDir = Vector2.Zero;
        KickPower = 0f;
        BallSpin = 0f;
    }

    public void CalculateInitialParameters()
    {
        _moveForce = Mass * LinearDamp * TopSpeed * PlayerSpeed;
        _rotateForce = Mass * AngularDamp * MaxAngularVelocity * PlayerSpeed;
        _currStamina = playerStamina;
    }

    public void SetPlayerStats(PlayerAI agent)
    {
        PlayerSpeed = agent.PlayerSpeed;
        playerStrength = agent.playerStrength;
        playerStamina = agent.playerStamina;
        PlayerName = agent.Name;

        CalculateInitialParameters();
    }

    public void SetPlayerStats(ClubPlayer agent)
    {
        PlayerSpeed = agent.PlayerSpeed;
        playerStrength = agent.playerStrength;
        playerStamina = agent.playerStamina;
        PlayerName = agent.Name;
        Agent = agent;

        CalculateInitialParameters();
    }

    public void SetPlayerNumber(int number)
    {
        PlayerNumber = number;
        PlayerNumberText = GetNode<RichTextLabel>("PlayerNumber");
        PlayerNumberText.Text = $"{number}";
    }

    public void SetPlayerColor(Color color)
    {
        ColorPrimary = color;
        var sprite = GetNode<Sprite2D>("Sprite2D");
        sprite.Modulate = color;
        SetPlayerNumberColor(new Color(1 - color.R, 1 - color.G, 1 - color.B));
    }

    public void SetBorderStaminaColor(float stamina)
    {
        stamina = (1/.6f) * (stamina - 0.4f);
        SetBorderColor(new Color(1-stamina, stamina, 0, 1));
    }

    public void SetBorderColor(Color color)
    {
        var border = GetNode<Sprite2D>("Border");
        border.Modulate = color;
    }

    public void SetPlayerNumberColor(Color color)
    {
        ColorSecondary = color;
        PlayerNumberText = GetNode<RichTextLabel>("PlayerNumber");
        PlayerNumberText.Modulate = color;
        SetBorderColor(color);
    }
}