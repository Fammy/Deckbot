using System;
using System.IO;
using Deckbot.Console.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Deckbot.Test;

[TestClass]
public class JsonTests
{
    [TestMethod]
    public void EmojiSerialize()
    {
        var writeObj = new BotReply
        {
            ReplyTime = DateTime.Now,
            CommentId = "1234",
            Reply = "String with 👀"
        };
        var writeJson = JsonConvert.SerializeObject(writeObj);
        File.WriteAllText("temp.json", writeJson);

        var readJson = File.ReadAllText("temp.json");
        var readObj = JsonConvert.DeserializeObject<BotReply>(readJson);

        Assert.AreEqual(writeObj.Reply, readObj.Reply);
    }
}