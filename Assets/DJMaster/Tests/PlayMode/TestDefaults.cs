using NUnit.Framework;

namespace DJMaster.Tests
{
    // The PlayMode tests start on the start menu, so the greeting (#50) is off unless a test turns it on. They also
    // don't wait out each prompt's reading time (#45); ReadingTimeTest turns that on.
    [SetUpFixture]
    public class TestDefaults
    {
        [OneTimeSetUp] public void Setup() { GameDirector.GreetOnStart = false; Reading.Enabled = false; }
        [OneTimeTearDown] public void Restore() { GameDirector.GreetOnStart = true; Reading.Enabled = true; }
    }
}
