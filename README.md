# Deckbot

Source behind https://www.reddit.com/user/deck_bot/

Please use Reddit bots responsibly.

## Setup instructions

1. Create a Reddit application on your [Apps page](https://www.reddit.com/prefs/apps/). Consider using a new reddit account for the bot otherwise things will post as you.
  1. Pick "script"
  1. In redirect URI, you can put  your OAuth server here or use [this service](https://not-an-aardvark.github.io/reddit-oauth-helper/) to get your refresh and access token
  1. Note the App ID. It's under the text "personal user script"
  1. Note the secret
1. Create two config files. They need to be in a folder called `config` the execution root (`.\bin\Debug\net6.0\config` if running in Visual Studio)

### config\config.json

Bot reads this at startup to get config values.

```
{
	"AppId": "", // Your AppId from your Reddit app
	"AppSecret": "", // Your secret from your Reddit app
	"RefreshToken": "", // Use your OAuth own server or https://not-an-aardvark.github.io/
	"AccessToken": "",// Use your OAuth own server or https://not-an-aardvark.github.io/

	"MessageRateLimitCooldown": 600, // If the bot is rate limited, wait this may seconds before trying to send a PM
	"MessageReplyCooldownMs": 10000, // Number of milliseconds (1000 = 1 second) to wait between posting replies to PMs

	"CommentRateLimitCooldown": 120, // If the bot is rate limited, wait this may seconds before trying to reply to a post comment
	"CommentReplyCooldownMs": 2000, // Number of milliseconds (1000 = 1 second) to wait between posting replies to comments

	"DownloadReservationDataFrequency": 15, // Number of seconds to download new reservation data from Google Sheet

	"BotStatusMessage": "", // Optional message to display at the beginning of messages. Limited to successful requests.

	"MonitorSubreddit": false, // If true, monitors all subreddits the user/bot is subscribed to for authorized posts in `PostsToMonitor` below. More work, but will find all comments and not top-level comments.
	"MonitorBotUserPosts": false, // Monitor all user/self posts by the bot
	"MonitorBotPrivateMessages": false, // Monitor bot's Private Messages
	"PostsToMonitor": [
		"ui642q" // Specific posts to monitor. Get from the post URL. Must have at least one or set `MonitorBotUserPosts` to true and have the bot make a self-post.
	]
}
```

### config\data.tsv

Data copied from https://docs.google.com/spreadsheets/d/1ZaKncig9fce7K0sr1f-E2_sgLH1HuKQ-q3k7clPMOCs/edit#gid=3349187&range=A1:C9

Bot will read this periodically, so it can be updated while running.

Should look like this and should have tabs between values

```
64	US	1626463177
256	US	1626459122
512	US	1626455986
64	UK	1626479155
256	UK	1626456352
512	UK	1626455694
64	EU	1626567000
256	EU	1626486563
512	EU	1626474939
```

## Notes

* When running in debug, the bot will respond to `!debugdeckbot` instead of `!deckbot`
* When the bot starts, it "flushes" all comments in the post so it won't respond to the same things twice. It still replies twice occasionally. It can miss new comments this way.
* Two folders are created at runtime: `logs` and `data`. Logs contains logs (duh) and data contains a cache of replies to send if the bot is rate limited.
* I threw this together quickly and was using it as a playground for some of the newer C# features. It's a little messier than I'd like, but that's how code goes.