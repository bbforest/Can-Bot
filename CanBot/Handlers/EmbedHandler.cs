using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CanBot.Handlers
{
    public static class EmbedHandler
    {
        /* This file is where we can store all the Embed Helper Tasks (So to speak). 
             We wrap all the creations of new EmbedBuilder's in a Task.Run to allow us to stick with Async calls. 
             All the Tasks here are also static which means we can call them from anywhere in our program. */
        public static async Task<Embed> CreateBasicEmbed(string title, string description, Color color)
        {
            var embed = await Task.Run(() => (new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .WithFooter("에케봇 By.파란대나무숲", "https://i.imgur.com/fWGVv2K.png").Build()));
            return embed;
        }

        public static async Task<Embed> CreateErrorEmbed(string source, string error)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle($"오류! - {source}")
                .WithDescription($"**오류내용**: \n{error}")
                .WithColor(Color.DarkRed)
                .WithFooter("에케봇 By.파란대나무숲", "https://i.imgur.com/fWGVv2K.png").Build());
            return embed;
        }
    }
}
