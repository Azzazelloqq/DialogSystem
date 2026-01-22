using DialogSystem.Runtime.Dsl;
using NUnit.Framework;

namespace DialogSystem.Tests
{
public sealed class DialogDslParserTests
{
    [Test]
    public void Parse_SimpleDialog_BuildsInstructions()
    {
        const string text = "@dialog main\n" +
                            "@label start\n" +
                            "Hero: Hello\n" +
                            "* \"Ask\" -> next\n" +
                            "@label next\n" +
                            "Narrator: End\n" +
                            "<<return>>\n";

        var result = DialogDslParser.Parse(text, "test");

        Assert.IsFalse(result.HasErrors);
        Assert.AreEqual(1, result.Dialogs.Count);
        var dialog = result.Dialogs[0];
        Assert.AreEqual("main", dialog.Id);
        Assert.AreEqual(0, dialog.EntryIndex);
        Assert.AreEqual(4, dialog.Instructions.Count);
    }

    [Test]
    public void Parse_InvalidExpression_ReportsError()
    {
        const string text = "@dialog main\n" +
                            "@label start\n" +
                            "* \"Ask\" when 1 + -> end\n";

        var result = DialogDslParser.Parse(text, "test");
        Assert.IsTrue(result.HasErrors);
    }
}
}
