using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CanBot.Services;
using System.Threading.Tasks;

namespace CanBot.Modules
{
    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        /* Get our AudioService from DI */
        public LavaLinkAudio AudioService { get; set; }

        /* All the below commands are ran via Lambda Expressions to keep this file as neat and closed off as possible. 
              We pass the AudioService Task into the section that would normally require an Embed as that's what all the
              AudioService Tasks are returning. */

        [Command("Join")]
        [Alias("입장")]
        public async Task JoinAndPlay()
            => await ReplyAsync(embed: await AudioService.JoinAsync(Context.Guild, Context.User as IVoiceState, Context.Channel as ITextChannel));

        [Command("Leave")]
        [Alias("퇴장", "나가")]
        public async Task Leave()
            => await ReplyAsync(embed: await AudioService.LeaveAsync(Context.Guild));

        [Command("Play")]
        [Alias("재생", "예약")]
        public async Task Play([Remainder] string search)
            => await ReplyAsync(embed: await AudioService.PlayAsync(Context.User as SocketGuildUser, Context.Guild, search));

        [Command("Stop")]
        [Alias("초기화")]
        public async Task Stop()
            => await ReplyAsync(embed: await AudioService.StopAsync(Context.Guild));

        [Command("List")]
        [Alias("목록", "재생목록")]
        public async Task List()
            => await ReplyAsync(embed: await AudioService.ListAsync(Context.Guild));

        [Command("Skip")]
        [Alias("넘기기", "스킵")]
        public async Task Skip()
            => await ReplyAsync(embed: await AudioService.SkipTrackAsync(Context.Guild));

        [Command("Volume")]
        [Alias("음량", "크기", "볼륨", "불륨")]
        public async Task Volume(int volume)
            => await ReplyAsync(await AudioService.SetVolumeAsync(Context.Guild, volume));

        [Command("Pause")]
        [Alias("Resume", "정지", "일시정지", "정지해제", "일시정지해제")]
        public async Task Pause()
            => await ReplyAsync(await AudioService.PARE(Context.Guild));
        //=> await ReplyAsync(await AudioService.PauseAsync(Context.Guild));

        /*
        [Command("Resume")]
        public async Task Resume()
            => await ReplyAsync(await AudioService.ResumeAsync(Context.Guild));
        */

        [Command("help")]
        [Alias("도움말", "도움")]
        public async Task Help()
            => await ReplyAsync(embed: await AudioService.Fun("Help"));

        [Command("corona")]
        [Alias("코로나", "covid", "코로나19", "covid19", "corona")]
        public async Task Corona()
            => await ReplyAsync(embed: await AudioService.Fun("COVID"));

        [Command("comic")]
        [Alias("웹툰", "만화", "webtoon", "comics")]
        public async Task Webtoon()
            => await ReplyAsync(embed: await AudioService.Fun("Webtoon"));

        [Command("eat")]
        [Alias("급식")]
        public async Task Eat(string school)
            => await ReplyAsync(embed: await AudioService.Fun("Eat", school));
    }
}
