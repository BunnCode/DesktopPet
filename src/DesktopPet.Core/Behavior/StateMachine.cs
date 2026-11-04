namespace DesktopPet.Core.Behavior;

internal sealed class StateMachine
{
    private readonly Dictionary<PetStateId, PetState> _states;

    public StateMachine(PetContext context, PetStateId initial)
    {
        PetState[] states =
        [
            new FallState(), new LandState(), new IdleState(), new WalkState(), new SitState(), new HeldState(), new HappyState(),
            new HangState(), new ShimmyState(), new SleepState(), new EatState(), new PlayState(),
            new ClimbState(), new ClingState(), new PushState(), new GoToFurnitureState(), new UseFurnitureState(),
            new CourtState(), new VisitState(),
        ];
        _states = states.ToDictionary(s => s.Id);
        Current = _states[initial];
        Current.Enter(context);
    }

    public PetState Current { get; private set; }

    /// <summary>Switches state and runs its Enter, even if it's the state already running (re-rolls its duration).</summary>
    public void ChangeTo(PetStateId id, PetContext context)
    {
        Current.Exit(context);
        Current = _states[id];
        Current.Enter(context);
    }

    public void Update(PetContext context, double dt)
    {
        if (Current.Update(context, dt) is { } next)
        {
            ChangeTo(next, context);
        }
    }
}
