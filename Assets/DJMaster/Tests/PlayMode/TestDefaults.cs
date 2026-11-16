using NUnit.Framework;

namespace DJMaster.Tests
{
    // The PlayMode tests start on the start menu, so the greeting (#50) is off unless a test turns it on.
    [SetUpFixture]
    public class TestDefaults
    {
        [OneTimeSetUp] public void Setup() => GameDirector.GreetOnStart = false;
        [OneTimeTearDown] public void Restore() => GameDirector.GreetOnStart = true;
    }
}
