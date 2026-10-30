namespace OhMyRudolph.Domain.Sessions;

public enum BoothStage { FrameSelection, Preview, Countdown, Review, Saving, Saved }
public enum BoothFrame { Light, Dark }
public enum BoothSaveResult { Cancelled, Failed, Succeeded }

/// <summary>UI-independent transitions. Image ownership belongs to the session's caller.</summary>
public sealed class BoothSession
{
    public const int ShotCount = 4;
    private int? _retakeSlot;

    public BoothStage Stage { get; private set; } = BoothStage.FrameSelection;
    public BoothFrame Frame { get; private set; } = BoothFrame.Light;
    public int ConfirmedCount { get; private set; }
    public int PendingSlot => _retakeSlot ?? ConfirmedCount;

    public void SelectFrame(BoothFrame frame)
    {
        Require(BoothStage.FrameSelection);
        if (!Enum.IsDefined(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
        Frame = frame;
    }

    public void BeginPreview()
    {
        Require(BoothStage.FrameSelection);
        Stage = BoothStage.Preview;
    }

    public void BeginCountdown(int? retakeSlot = null)
    {
        if (retakeSlot is { } slot)
        {
            if (slot < 0 || slot >= ShotCount) throw new ArgumentOutOfRangeException(nameof(retakeSlot));
            Require(BoothStage.Review);
        }
        else
        {
            Require(BoothStage.Preview);
        }
        _retakeSlot = retakeSlot;
        Stage = BoothStage.Countdown;
    }

    public int ConfirmShot()
    {
        Require(BoothStage.Countdown);
        var slot = PendingSlot;
        if (_retakeSlot is null) ConfirmedCount++;
        _retakeSlot = null;
        Stage = ConfirmedCount == ShotCount ? BoothStage.Review : BoothStage.Preview;
        return slot;
    }

    public void CancelCountdown()
    {
        Require(BoothStage.Countdown);
        Stage = _retakeSlot is null ? BoothStage.Preview : BoothStage.Review;
        _retakeSlot = null;
    }

    public void BeginSave()
    {
        Require(BoothStage.Review);
        Stage = BoothStage.Saving;
    }

    public void FinishSave(BoothSaveResult result)
    {
        Require(BoothStage.Saving);
        if (!Enum.IsDefined(result)) throw new ArgumentOutOfRangeException(nameof(result));
        Stage = result == BoothSaveResult.Succeeded ? BoothStage.Saved : BoothStage.Review;
    }

    public void Reset()
    {
        Stage = BoothStage.FrameSelection;
        Frame = BoothFrame.Light;
        ConfirmedCount = 0;
        _retakeSlot = null;
    }

    private void Require(BoothStage expected)
    {
        if (Stage != expected) throw new InvalidOperationException($"Cannot transition from {Stage}; expected {expected}.");
    }
}
