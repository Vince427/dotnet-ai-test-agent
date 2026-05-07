using DesktopAiTestAgent.AgentRunner;
using DesktopAiTestAgent.Core;

namespace DesktopAiTestAgent.AgentRunner.Tests;

public sealed class QualityGuardsTests
{
    [Fact]
    public void UiTreeGuardPassesWhenElementsRemainVisible()
    {
        var guard = new UiTreeQualityGuard();
        var result = guard.Check(CreateContext(new FakeDriver(new UiSnapshot("Window", [new UiElement { Name = "Login" }]))));

        Assert.Equal(QualityGuardStatus.Passed, result.Status);
    }

    [Fact]
    public void UiTreeGuardForceRejectsWhenTreeIsEmpty()
    {
        var guard = new UiTreeQualityGuard();
        var result = guard.Check(CreateContext(new FakeDriver(new UiSnapshot("Window", []))));

        Assert.Equal(QualityGuardStatus.ForceReject, result.Status);
        Assert.Equal("uia_tree_empty", result.Code);
    }

    [Fact]
    public void UiTreeGuardAbortsWhenCaptureFails()
    {
        var guard = new UiTreeQualityGuard();
        var result = guard.Check(CreateContext(new FakeDriver(new InvalidOperationException("window closed"))));

        Assert.Equal(QualityGuardStatus.Abort, result.Status);
        Assert.Equal("uia_capture_failed", result.Code);
        Assert.Contains("window closed", result.Message);
    }

    private static QualityGuardContext CreateContext(IAutomationDriver driver)
    {
        return new QualityGuardContext
        {
            StepNumber = 1,
            Driver = driver,
            SnapshotBefore = new UiSnapshot("Window", [new UiElement { Name = "Before" }]),
            Action = new AgentAction { ActionType = "Click", AutomationId = "btnLogin" },
            Goal = new AgentGoal { Description = "Log in" }
        };
    }

    private sealed class FakeDriver : IAutomationDriver
    {
        private readonly UiSnapshot? _snapshot;
        private readonly Exception? _captureException;

        public FakeDriver(UiSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public FakeDriver(Exception captureException)
        {
            _captureException = captureException;
        }

        public bool AttachToWindow(string windowTitle, TimeSpan timeout) => true;

        public UiSnapshot Capture()
        {
            if (_captureException != null)
                throw _captureException;
            return _snapshot!;
        }

        public void EnterText(string automationId, string value) { }
        public void Click(string automationId) { }
        public string ReadText(string automationId) => "";
        public List<UiElement> GetAllElements() => _snapshot?.Elements ?? [];
        public byte[] CaptureScreenshot() => [];
        public void Scroll(string automationId, string direction) { }
        public void DoubleClick(string automationId) { }
    }
}
