// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Models;
using SchemaHammer.Services;

namespace SchemaHammer.UnitTests;

public class NavigationServiceTests
{
    [Test]
    public void CanGoBack_FalseWhenEmpty()
    {
        var service = new NavigationService();
        Assert.That(service.CanGoBack, Is.False);
    }

    [Test]
    public void Push_ThenCanGoBack()
    {
        var service = new NavigationService();
        service.Push(new TreeNodeModel { Text = "A" });
        Assert.That(service.CanGoBack, Is.True);
    }

    [Test]
    public void Pop_ReturnsLastPushed()
    {
        var service = new NavigationService();
        var a = new TreeNodeModel { Text = "A" };
        var b = new TreeNodeModel { Text = "B" };
        service.Push(a);
        service.Push(b);

        Assert.That(service.Pop(), Is.SameAs(b));
        Assert.That(service.Pop(), Is.SameAs(a));
    }

    [Test]
    public void Pop_ReturnsNullWhenEmpty()
    {
        var service = new NavigationService();
        Assert.That(service.Pop(), Is.Null);
    }

    [Test]
    public void Push_CapsAtMaxHistory()
    {
        var service = new NavigationService();
        for (var i = 0; i < 25; i++)
            service.Push(new TreeNodeModel { Text = $"Node{i}" });

        Assert.That(service.History, Has.Count.EqualTo(20));
        Assert.That(service.History[0].Text, Is.EqualTo("Node5"));
    }

    [Test]
    public void NavigateTo_ReturnsNodeAndTruncates()
    {
        var service = new NavigationService();
        var a = new TreeNodeModel { Text = "A" };
        var b = new TreeNodeModel { Text = "B" };
        var c = new TreeNodeModel { Text = "C" };
        service.Push(a);
        service.Push(b);
        service.Push(c);

        var result = service.NavigateTo(1);

        Assert.That(result, Is.SameAs(b));
        Assert.That(service.History, Has.Count.EqualTo(1));
        Assert.That(service.History[0].Text, Is.EqualTo("A"));
    }

    [Test]
    public void NavigateTo_ReturnsNullForInvalidIndex()
    {
        var service = new NavigationService();
        Assert.That(service.NavigateTo(-1), Is.Null);
        Assert.That(service.NavigateTo(0), Is.Null);
    }

    [Test]
    public void Clear_EmptiesHistory()
    {
        var service = new NavigationService();
        service.Push(new TreeNodeModel { Text = "A" });
        service.Push(new TreeNodeModel { Text = "B" });

        service.Clear();

        Assert.That(service.CanGoBack, Is.False);
        Assert.That(service.History, Has.Count.EqualTo(0));
    }
}
