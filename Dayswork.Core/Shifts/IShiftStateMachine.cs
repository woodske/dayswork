namespace Dayswork.Core.Shifts;

public interface IShiftStateMachine
{
    ShiftPhase Phase { get; }
    ShiftIntent? CurrentIntent { get; }
    void Transition(ShiftPhase newPhase, ShiftIntent? intent = null);
    void SetIntent(ShiftIntent intent);
}
