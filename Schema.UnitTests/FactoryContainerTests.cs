using Schema.Isolators;
using Schema.DataAccess;
using System.Collections.Generic;

namespace Schema.UnitTests;

public class FactoryContainerTests
{
    [Test]
    public void ClearShouldRemoveAllRegistrations()
    {
        var test = new object();
        var test2 = new string[1];
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(test);
            FactoryContainer.Register(test2);

            Assert.Multiple(() =>
            {
                Assert.That(FactoryContainer.Resolve<object>(), Is.Not.Null);
                Assert.That(FactoryContainer.Resolve<string[]>(), Is.Not.Null);
            });

            FactoryContainer.Clear();
            Assert.Multiple(() =>
            {
                Assert.That(FactoryContainer.Resolve<object>(), Is.Null);
                Assert.That(FactoryContainer.Resolve<string[]>(), Is.Null);
            });
        }
    }

    [Test]
    public void ShouldRegisterObjectByType()
    {
        var test = new object();
        lock (FactoryContainer.SharedLockObject)
        {
            FactoryContainer.Register(test);
            var getTest = FactoryContainer.ResolveOrCreate<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldCreateSimpleObjectIfTypeNotRegistered()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.ResolveOrCreate<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
            });

            var getTest2 = FactoryContainer.ResolveOrCreate<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest2, Is.Not.Null);
                Assert.That(getTest2, Is.TypeOf<object>());
                Assert.That(getTest2, Is.Not.SameAs(getTest));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldCreateSimpleObjectIfTypeNotRegisteredAndRegisterItWhenRequested()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.ResolveOrCreate<object>(true);
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
            });

            var getTest2 = FactoryContainer.ResolveOrCreate<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest2, Is.Not.Null);
                Assert.That(getTest2, Is.TypeOf<object>());
                Assert.That(getTest2, Is.SameAs(getTest));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldReturnNullWhenRequestedTypeNotFound()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.Resolve<object>();
            Assert.That(getTest, Is.Null);
        }
    }

    [Test]
    public void ShouldReturnRegisteredObjectWhenRequested()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var test = new object();
            FactoryContainer.Register(test);

            var getTest = FactoryContainer.Resolve<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldFindRegisteredInterface()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            IEnumerable<string> test = new string[10];
            FactoryContainer.Register(test);

            var getTest = FactoryContainer.Resolve<IEnumerable<string>, string[]>() as string[];
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<string[]>());
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldFindRegisteredInterfaceBeforeType()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            IEnumerable<string> test = new string[10];
            var test2 = new string[10];
            FactoryContainer.Register(test); // registering by interface
            FactoryContainer.Register(test2); // registering by type

            var getTest = FactoryContainer.Resolve<IEnumerable<string>, string[]>() as string[];
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<string[]>());
                Assert.That(getTest, Is.Not.SameAs(test2));
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldCreateSpecifiedTypeIfInterfaceNotFound()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.ResolveOrCreate<ISqlConnectionFactory, SqlConnectionFactory>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.AssignableTo<ISqlConnectionFactory>());
                Assert.That(getTest, Is.TypeOf<SqlConnectionFactory>());
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldCreateAndRegisterSpecifiedTypeIfInterfaceNotFound()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.ResolveOrCreate<ISqlConnectionFactory, SqlConnectionFactory>(true);
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.AssignableTo<ISqlConnectionFactory>());
                Assert.That(getTest, Is.TypeOf<SqlConnectionFactory>());
            });

            var getTest2 = FactoryContainer.ResolveOrCreate<ISqlConnectionFactory>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest2, Is.Not.Null);
                Assert.That(getTest2, Is.AssignableTo<ISqlConnectionFactory>());
                Assert.That(getTest, Is.SameAs(getTest2));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldFindRatherThanCreateRegisteredInterface()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            IEnumerable<string> test = new string[10];
            FactoryContainer.Register(test);

            var getTest = FactoryContainer.ResolveOrCreate<IEnumerable<string>, string[]>() as string[];
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<string[]>());
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldFindRatherThanCreateRegisteredInterfaceBeforeType()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            IEnumerable<string> test = new string[10];
            var test2 = new string[10];
            FactoryContainer.Register(test); // registering by interface
            FactoryContainer.Register(test2); // registering by type

            var getTest = FactoryContainer.ResolveOrCreate<IEnumerable<string>, string[]>() as string[];
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<string[]>());
                Assert.That(getTest, Is.Not.SameAs(test2));
                Assert.That(getTest, Is.SameAs(test));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldFindRatherThanCreateRegisteredTypeWhenInterfaceNotRegistered()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var test2 = new string[10];
            FactoryContainer.Register(test2); // registering by type

            var getTest = FactoryContainer.ResolveOrCreate<IEnumerable<string>, string[]>() as string[];
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<string[]>());
                Assert.That(getTest, Is.SameAs(test2));
            });

            FactoryContainer.Clear();
        }
    }

    [Test]
    public void ShouldReturnNullWhenRequestedTypeAndInterfaceNotFound()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var getTest = FactoryContainer.Resolve<ISqlConnectionFactory, SqlConnectionFactory>();
            Assert.That(getTest, Is.Null);
        }
    }

    [Test]
    public void RegisteringANewObjectOfTheSameTypeShouldReplaceTheOriginalRegisteredObject()
    {
        lock (FactoryContainer.SharedLockObject)
        {
            var test = new object();
            FactoryContainer.Register(test);

            var getTest = FactoryContainer.Resolve<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
                Assert.That(getTest, Is.SameAs(test));
            });

            var test2 = new object();
            FactoryContainer.Register(test2);

            getTest = FactoryContainer.Resolve<object>();
            Assert.Multiple(() =>
            {
                Assert.That(getTest, Is.Not.Null);
                Assert.That(getTest, Is.TypeOf<object>());
                Assert.That(getTest, Is.Not.SameAs(test));
                Assert.That(getTest, Is.SameAs(test2));
            });

            FactoryContainer.Clear();
        }
    }
}
