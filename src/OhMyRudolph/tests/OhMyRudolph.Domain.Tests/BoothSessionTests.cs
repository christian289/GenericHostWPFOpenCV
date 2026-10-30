using OhMyRudolph.Domain.Sessions;

namespace OhMyRudolph.Tests;

public sealed class BoothSessionTests
{
    [Fact]
    public void CancelledCountdownDoesNotConfirmShotAndCanResume()
    {
        var session = new BoothSession();
        session.BeginPreview();
        session.BeginCountdown();
        session.CancelCountdown();

        Assert.Equal(BoothStage.Preview, session.Stage);
        Assert.Equal(0, session.ConfirmedCount);
        session.BeginCountdown();
        Assert.Equal(0, session.ConfirmShot());
        Assert.Equal(1, session.ConfirmedCount);
    }

    [Fact]
    public void FourthConfirmationEntersReviewRatherThanAutoSaving()
    {
        var session = CompleteSession();

        Assert.Equal(BoothStage.Review, session.Stage);
        Assert.Equal(4, session.ConfirmedCount);
        Assert.Throws<InvalidOperationException>(() => session.BeginCountdown());
    }

    [Fact]
    public void FinalShotRetakeReplacesOnlyFinalSlot()
    {
        var session = CompleteSession();
        session.BeginCountdown(3);
        Assert.Equal(3, session.ConfirmShot());

        Assert.Equal(4, session.ConfirmedCount);
        Assert.Equal(BoothStage.Review, session.Stage);
        session.BeginSave();
        session.FinishSave(BoothSaveResult.Succeeded);
        Assert.Equal(BoothStage.Saved, session.Stage);
    }

    [Fact]
    public void CancelledRetakePreservesCompleteReview()
    {
        var session = CompleteSession();
        session.BeginCountdown(3);
        session.CancelCountdown();

        Assert.Equal(4, session.ConfirmedCount);
        Assert.Equal(BoothStage.Review, session.Stage);
        session.BeginSave();
        Assert.Equal(BoothStage.Saving, session.Stage);
    }

    [Theory]
    [InlineData(BoothSaveResult.Cancelled)]
    [InlineData(BoothSaveResult.Failed)]
    public void UnsuccessfulSaveRetainsAllShotsAndAllowsRetry(BoothSaveResult result)
    {
        var session = CompleteSession();
        session.BeginSave();
        session.FinishSave(result);

        Assert.Equal(BoothStage.Review, session.Stage);
        Assert.Equal(4, session.ConfirmedCount);
        session.BeginSave();
        session.FinishSave(BoothSaveResult.Succeeded);
        Assert.Equal(BoothStage.Saved, session.Stage);
    }

    [Fact]
    public void ResetClearsSavedOrPartialSessionAndRestoresFrameChoice()
    {
        var session = CompleteSession();
        session.BeginSave();
        session.FinishSave(BoothSaveResult.Succeeded);
        session.Reset();

        Assert.Equal(BoothStage.FrameSelection, session.Stage);
        Assert.Equal(0, session.ConfirmedCount);
        session.SelectFrame(BoothFrame.Dark);
        session.BeginPreview();
        session.BeginCountdown();
        session.ConfirmShot();
        session.Reset();
        Assert.Equal(0, session.ConfirmedCount);
        Assert.Equal(BoothStage.FrameSelection, session.Stage);
        Assert.Equal(BoothFrame.Light, session.Frame);
    }

    [Fact]
    public void CancellingThirdShotKeepsFirstTwoAndResumesAtThird()
    {
        var session = new BoothSession();
        session.BeginPreview();
        session.BeginCountdown();
        session.ConfirmShot();
        session.BeginCountdown();
        session.ConfirmShot();
        session.BeginCountdown();
        session.CancelCountdown();

        Assert.Equal(2, session.ConfirmedCount);
        Assert.Equal(BoothStage.Preview, session.Stage);
        session.BeginCountdown();
        Assert.Equal(2, session.ConfirmShot());
        Assert.Equal(3, session.ConfirmedCount);
    }

    [Fact]
    public void SavingAndRetakingCannotBypassFourConfirmedPhotos()
    {
        var session = new BoothSession();
        Assert.Throws<InvalidOperationException>(() => session.BeginSave());
        session.BeginPreview();
        Assert.Throws<InvalidOperationException>(() => session.BeginSave());
        Assert.Throws<InvalidOperationException>(() => session.BeginCountdown(3));
        session.BeginCountdown();
        Assert.Throws<InvalidOperationException>(() => session.SelectFrame(BoothFrame.Dark));
        Assert.Throws<InvalidOperationException>(() => session.BeginCountdown());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void RetakeRejectsOutOfRangeSlotsWithoutLosingReview(int slot)
    {
        var session = CompleteSession();
        Assert.Throws<ArgumentOutOfRangeException>(() => session.BeginCountdown(slot));
        Assert.Equal(BoothStage.Review, session.Stage);
        Assert.Equal(4, session.ConfirmedCount);
    }

    private static BoothSession CompleteSession()
    {
        var session = new BoothSession();
        session.BeginPreview();
        for (var i = 0; i < 4; i++)
        {
            session.BeginCountdown();
            Assert.Equal(i, session.ConfirmShot());
        }
        return session;
    }
}
