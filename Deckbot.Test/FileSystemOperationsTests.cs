using System;
using Deckbot.Console;
using Deckbot.Console.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Deckbot.Test;

[TestClass]
public class FileSystemOperationsTests
{
    [TestMethod]
    public void Append()
    {
        var reply = new BotReply
        {
            ReplyTime = DateTime.Now,
            Reply = "One two three",
            CommentId = "123"
        };

        try
        {
            throw new ApplicationException("Testing");
        }
        catch (Exception ex)
        {
            FileSystemOperations.WriteException("unit_test", reply, ex);
        }
    }
}