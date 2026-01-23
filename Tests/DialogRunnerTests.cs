using DialogSystem.Runtime;
using DialogSystem.Runtime.Dsl;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests
{
public sealed class DialogRunnerTests
{
    [Test]
    public void Runner_IfElse_SelectsTrueBranch()
    {
        const string text = "@dialog main\n" +
                            "@label start\n" +
                            "<<set Flag = true>>\n" +
                            "<<if Flag>>\n" +
                            "Narrator: Yes\n" +
                            "<<else>>\n" +
                            "Narrator: No\n" +
                            "<<endif>>\n" +
                            "<<return>>\n";

        var result = DialogDslParser.Parse(text, "test");
        var asset = ScriptableObject.CreateInstance<DialogAsset>();
        asset.SetData("test", result.Dialogs, result.Errors);

        var context = new DialogContext();
        var runner = new DialogRunner(asset, context);
        var evt = runner.Start("main");

        Assert.AreEqual(DialogEventType.Line, evt.Type);
        Assert.AreEqual("Yes", evt.Line.Text);
        evt = runner.Advance();
        Assert.AreEqual(DialogEventType.End, evt.Type);
    }

    [Test]
    public void Runner_Choices_SelectsTarget()
    {
        const string text = "@dialog main\n" +
                            "@label start\n" +
                            "* \"A\" -> a\n" +
                            "* \"B\" -> b\n" +
                            "@label a\n" +
                            "Narrator: A\n" +
                            "<<return>>\n" +
                            "@label b\n" +
                            "Narrator: B\n" +
                            "<<return>>\n";

        var result = DialogDslParser.Parse(text, "test");
        var asset = ScriptableObject.CreateInstance<DialogAsset>();
        asset.SetData("test", result.Dialogs, result.Errors);

        var runner = new DialogRunner(asset, new DialogContext());
        var evt = runner.Start("main");

        Assert.AreEqual(DialogEventType.Choices, evt.Type);
        Assert.AreEqual(2, evt.Choices.Options.Count);

        evt = runner.Choose(1);
        Assert.AreEqual(DialogEventType.Line, evt.Type);
        Assert.AreEqual("B", evt.Line.Text);
    }

    [Test]
    public void Runner_ExitCommand_ReturnsOutcome()
    {
        const string text = "@dialog main\n" +
                            "@label start\n" +
                            "<<exit Angry>>\n";

        var result = DialogDslParser.Parse(text, "test");
        var asset = ScriptableObject.CreateInstance<DialogAsset>();
        asset.SetData("test", result.Dialogs, result.Errors);

        var runner = new DialogRunner(asset, new DialogContext());
        var evt = runner.Start("main");

        Assert.AreEqual(DialogEventType.Outcome, evt.Type);
        Assert.AreEqual("Angry", evt.Outcome);
    }
}
}
