using System.Text.RegularExpressions;
using Deckbot.Console;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Deckbot.Test
{
    [TestClass]
    public class RegExTests
    {
        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void RegionModelTimeStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {region} {model} {reserveTime}", RegexConsts.RegionModelTime, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void ModelRegionTimeStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {model} {region} {reserveTime}", RegexConsts.ModelRegionTime, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void TimeModelRegionStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {reserveTime} {model} {region}", RegexConsts.TimeModelRegion, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void ModelTimeRegionStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {model} {reserveTime} {region}", RegexConsts.ModelTimeRegion, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void RegionTimeModelStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {region} {reserveTime} {model}", RegexConsts.RegionTimeModel, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("US", "512", "1626412345")]
        [DataRow("EU", "64", "1626412345")]
        [DataRow("UK", "256", "1626412345")]
        [DataRow("us", "512", "1626412345")]
        [DataRow("eu", "64", "1626412345")]
        [DataRow("uk", "256", "1626412345")]
        [DataRow("UK", "256GB", "1626412345")]
        [DataRow("UK", "256gb", "1626412345")]
        [DataRow("UK", "256GB", "rt1626412345")]
        public void TimeRegionModelStrict(string region, string model, string reserveTime)
        {
            Assert.IsTrue(Regex.IsMatch($"!deckbot {reserveTime} {region} {model}", RegexConsts.TimeRegionModel, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("!deckbot I am a US 512 1626412345")]
        [DataRow("!deckbot I am a US 512GB 1626412345")]
        [DataRow("!deckbot I am a US 512gb 1626412345")]
        [DataRow("!deckbot I am a US 512 GB 1626412345")]
        [DataRow("!deckbot I am a US 512 with rt1626412345")]
        public void RegionModelTimeFuzzy(string message)
        {
            Assert.IsTrue(Regex.IsMatch(message, RegexConsts.RegionModelTime, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("!deckbot I am a 512 US 1626412345")]
        [DataRow("!deckbot I am a 512GB US 1626412345")]
        [DataRow("!deckbot I am a 512gb US 1626412345")]
        [DataRow("!deckbot I am a 512 GB US 1626412345")]
        [DataRow("!deckbot I am a 512 US with rt1626412345")]
        public void ModelRegionTimeFuzzy(string message)
        {
            Assert.IsTrue(Regex.IsMatch(message, RegexConsts.ModelRegionTime, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        [DataRow("!deckbot I am a 512 1626412345 US")]
        [DataRow("!deckbot I am a 512GB 1626412345 US")]
        [DataRow("!deckbot I am a 512gb 1626412345 US")]
        [DataRow("!deckbot I am a 512 GB 1626412345 US")]
        [DataRow("!deckbot I am a 512 with rt1626412345 US")]
        public void ModelTimeRegionFuzzy(string message)
        {
            Assert.IsTrue(Regex.IsMatch(message, RegexConsts.ModelTimeRegion, RegexOptions.IgnoreCase));
        }


        [TestMethod]
        [DataRow("!deckbot I am a US 1626412345 512")]
        [DataRow("!deckbot I am a US 1626412345 512GB")]
        [DataRow("!deckbot I am a US 1626412345 512gb")]
        [DataRow("!deckbot I am a GB US 1626412345 512")]
        [DataRow("!deckbot I am a US with rt1626412345 512")]
        public void RegionTimeModelFuzzy(string message)
        {
            Assert.IsTrue(Regex.IsMatch(message, RegexConsts.RegionTimeModel, RegexOptions.IgnoreCase));
        }

        [TestMethod]
        public void RegionModelTime_Invalid()
        {
            Assert.IsFalse(Regex.IsMatch("US 512 1626412345", RegexConsts.RegionModelTime, RegexOptions.IgnoreCase));
            Assert.IsFalse(Regex.IsMatch("!deckbot AB 512 1626412345", RegexConsts.RegionModelTime, RegexOptions.IgnoreCase));
            Assert.IsFalse(Regex.IsMatch("!deckbot US 512 162641", RegexConsts.RegionModelTime, RegexOptions.IgnoreCase));
        }
    }
}