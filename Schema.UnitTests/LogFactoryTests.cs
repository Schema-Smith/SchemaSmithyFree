using Schema.Utility;

namespace Schema.UnitTests;

public class LogFactoryTests
{
    [Test]
    public void ShouldGetDefaultLoggerIfNoneConfigured()
    {
        var test = LogFactory.GetLogger("TEST");
        Assert.That(test, Is.Not.Null);
    }

    [Test]
    public void ShouldReplaceRegistration()
    {
        var test = LogFactory.GetLogger("TEST");
        var test2 = LogFactory.GetLogger("TEST2");
        LogFactory.Register("TEST", test2);

        Assert.Multiple(() =>
        {
            Assert.That(test, Is.Not.SameAs(LogFactory.GetLogger("TEST")));
            Assert.That(test2, Is.SameAs(LogFactory.GetLogger("TEST")));
        });
    }
}
